using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StarMap.Models;

namespace StarMap.Data
{
    /// <summary>
    /// JPL Small-Body Database (SBDB) client. Fetches osculating orbital elements and
    /// physical data (diameter, class) for asteroids and comets, normalized to
    /// <see cref="BodyInfo"/> and cached to %LocalAppData%\StarMap\sbdb-cache.json so
    /// the app still shows small bodies when offline.
    /// </summary>
    public static class SbdbClient
    {
        private const string BaseUrl = "https://ssd-api.jpl.nasa.gov/sbdb.api";
        private const double MJD_J2000 = 51544.5; // MJD of J2000 (JD 2451545.0)

        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

        private static readonly JsonSerializerOptions _jsonOpts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };

        private static readonly string[] CatalogIds =
        {
            "Bennu", "Apophis", "Didymos", "2024 PT5", "Ryugu", "Itokawa",
            "Ceres", "Vesta", "Pallas", "Hygiea", "Eros",
            "1P", "67P",
        };

        private static string CachePath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "StarMap", "sbdb-cache.json");

        /// <summary>
        /// Returns the small-body catalog. Loads from cache first for instant startup,
        /// then refreshes from JPL when <paramref name="forceRefresh"/> is true (or the
        /// cache is missing/expired).
        /// </summary>
        public static async Task<List<BodyInfo>> FetchSmallBodiesAsync(bool forceRefresh = false)
        {
            var cached = LoadCache();
            if (cached != null && !forceRefresh)
                return cached;

            var fetched = await FetchFromJplAsync();
            if (fetched != null)
            {
                SaveCache(fetched);
                return fetched;
            }

            return cached ?? new List<BodyInfo>();
        }

        private static async Task<List<BodyInfo>?> FetchFromJplAsync()
        {
            var result = new List<BodyInfo>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));

            foreach (var id in CatalogIds)
            {
                try
                {
                    var url = $"{BaseUrl}?sstr={Uri.EscapeDataString(id)}";
                    var json = await _http.GetStringAsync(url, cts.Token);
                    var body = ParseSmallBody(id, json);
                    if (body != null) result.Add(body);
                }
                catch (Exception ex)
                {
                    App.Log.Info($"SBDB fetch '{id}' failed: {ex.Message}");
                }
            }

            if (result.Count == 0) return null;
            return result;
        }

        private static BodyInfo? ParseSmallBody(string id, string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("object", out var obj) ||
                !root.TryGetProperty("orbit", out var orbit))
                return null;

            // SBDB returns orbital elements as a list of { name, value } entries.
            var els = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            if (orbit.TryGetProperty("elements", out var elList) && elList.ValueKind == JsonValueKind.Array)
            {
                foreach (var elem in elList.EnumerateArray())
                {
                    if (elem.TryGetProperty("name", out var n) && n.GetString() is string name &&
                        TryGetDouble(elem, "value", out var v))
                        els[name.ToLowerInvariant()] = v;
                }
            }

            if (!els.TryGetValue("a", out var aAU) || !els.TryGetValue("e", out var ecc))
                return null; // skip parabolic/unresolved orbits

            string fullName = GetString(obj, "fullname") ?? id;
            bool isComet = fullName.Contains('/');
            string sClass = GetString(obj, "orbit_class", "code") ?? GetString(obj, "class") ?? "NEA";

            // epoch comes from orbit.epoch (Julian Date)
            double epochJD = GetDouble(orbit, "epoch") ?? 2451545.0;
            double epochDays = epochJD - 2451545.0;

            double nMean = els.GetValueOrDefault("n", 0.9856076686 / Math.Pow(aAU, 1.5));
            double m0;
            if (els.TryGetValue("ma", out var ma))
            {
                m0 = ma;
            }
            else if (els.TryGetValue("tp", out var tp))
            {
                m0 = nMean * (epochJD - tp);
            }
            else
            {
                m0 = 0;
            }

            double diameterKm = 0;
            double hMag = 0;
            if (obj.TryGetProperty("phys_par", out var phys))
            {
                diameterKm = GetPhysValue(phys, "diameter") ?? 0;
                hMag = GetPhysValue(phys, "H") ?? 0;
                if (diameterKm <= 0 && hMag > 0)
                {
                    double albedo = GetPhysValue(phys, "albedo") ?? 0.14;
                    diameterKm = 1329.0 * Math.Pow(10, -hMag / 5.0) / Math.Sqrt(Math.Max(albedo, 0.01));
                }
            }

            var periodDays = 365.256898326 * Math.Pow(aAU, 1.5);
            var body = new BodyInfo
            {
                Id = id.ToLowerInvariant().Replace(' ', '-'),
                Name = fullName,
                Kind = isComet ? "comet" : "asteroid",
                Parent = "sun",
                RadiusKm = diameterKm > 0 ? diameterKm / 2.0 : 5.0,
                VisualRadius = Math.Clamp(diameterKm > 0 ? diameterKm * 0.0009 : 0.1, 0.1, 1.0),
                Color = isComet ? "#bfe8ff" : "#b9b4ab",
                DiameterKm = diameterKm > 0 ? diameterKm : null,
                SbdbClass = sClass,
                TrailDays = Math.Min(2 * periodDays, 3000),
                Elements = new OrbitalElements
                {
                    EpochDays = epochDays,
                    A = aAU, Adot = 0,
                    E = Math.Min(ecc, 0.9999), Edot = 0,
                    I = els.GetValueOrDefault("i"), Idot = 0,
                    Raan = els.GetValueOrDefault("om"), Raandot = 0,
                    Wp = els.GetValueOrDefault("w"), Wpdot = 0,
                    M0 = m0, Mdot = nMean,
                },
                Description = $"JPL Small-Body Database object ({sClass})." +
                              (diameterKm > 0 ? $" Diameter ≈ {diameterKm:F1} km." : ""),
            };

            return body;
        }

        /// <summary>Reads a physical parameter by name from SBDB's phys_par (list or object).</summary>
        private static double? GetPhysValue(JsonElement phys, string name)
        {
            if (phys.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in phys.EnumerateArray())
                {
                    if (item.TryGetProperty("name", out var n) && n.GetString() == name &&
                        TryGetDouble(item, "value", out var v))
                        return v;
                }
                return null;
            }

            if (phys.ValueKind == JsonValueKind.Object && phys.TryGetProperty(name, out var sub))
            {
                if (TryGetDouble(sub, out var direct)) return direct;
                if (sub.ValueKind == JsonValueKind.Array && sub.GetArrayLength() > 0)
                {
                    var first = sub[0];
                    if (first.TryGetProperty("value", out var v) && TryGetDouble(v, out var d)) return d;
                    if (TryGetDouble(first, out var d2)) return d2;
                }
                if (sub.ValueKind == JsonValueKind.Object && sub.TryGetProperty("value", out var v2) &&
                    TryGetDouble(v2, out var d3))
                    return d3;
            }
            return null;
        }

        private static List<BodyInfo>? LoadCache()
        {
            try
            {
                if (!File.Exists(CachePath)) return null;
                var json = File.ReadAllText(CachePath);
                return JsonSerializer.Deserialize<List<BodyInfo>>(json, _jsonOpts);
            }
            catch (Exception ex)
            {
                App.Log.Info($"SBDB cache load failed: {ex.Message}");
                return null;
            }
        }

        private static void SaveCache(List<BodyInfo> bodies)
        {
            try
            {
                var dir = Path.GetDirectoryName(CachePath);
                if (dir != null) Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(bodies, _jsonOpts);
                File.WriteAllText(CachePath, json);
            }
            catch (Exception ex)
            {
                App.Log.Info($"SBDB cache save failed: {ex.Message}");
            }
        }

        private static string? GetString(JsonElement obj, params string[] path)
        {
            JsonElement current = obj;
            foreach (var key in path)
            {
                if (!current.TryGetProperty(key, out current)) return null;
            }
            return current.ValueKind == JsonValueKind.String ? current.GetString() : current.ToString();
        }

        private static double? GetDouble(JsonElement obj, params string[] path)
        {
            JsonElement current = obj;
            foreach (var key in path)
            {
                if (!current.TryGetProperty(key, out current)) return null;
            }
            return TryGetDouble(current, out var d) ? d : null;
        }

        private static bool TryGetDouble(JsonElement el, string prop, out double value)
        {
            if (el.TryGetProperty(prop, out var v)) return TryGetDouble(v, out value);
            value = 0;
            return false;
        }

        private static bool TryGetDouble(JsonElement v, out double value)
        {
            if (v.ValueKind == JsonValueKind.Number)
            {
                value = v.GetDouble();
                return true;
            }
            if (v.ValueKind == JsonValueKind.String &&
                double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return true;
            value = 0;
            return false;
        }
    }
}
