using System;
using System.Collections.Generic;
using StarMap.Models;

namespace StarMap.Data
{
    /// <summary>
    /// Bundled solar-system catalog. Planets/Pluto use the JPL "Approximate Positions of the
    /// Planets" mean elements (J2000, secular rates per century); the Moon uses Meeus mean
    /// orbital elements. Bodies are the source of truth for both the native inspector and the
    /// web renderer (serialized to JSON).
    /// </summary>
    public static class BodyCatalog
    {
        private const double DaysPerCentury = 36525.0;
        private const double AuKm = 149597870.7;

        public static List<BodyInfo> All { get; } = BuildCatalog();

        public static BodyInfo? Find(string id) =>
            All.Find(b => string.Equals(b.Id, id, StringComparison.OrdinalIgnoreCase));

        private static List<BodyInfo> BuildCatalog()
        {
            var list = new List<BodyInfo>
            {
                Star(),
                Planet("mercury", "Mercury", 2439.7, 3.3011e23, 1407.6, 0.034, 0.75, "#b8b4a8",
                    "mercury.jpg", null, null, 30,
                    0.38709843, 0, 0.20563661, 0.00002123, 7.00498625, -0.00594749,
                    252.25032399, 149472.67411175, 77.45611903, 0.15930268, 48.33054661, -0.12501721,
                    null, false, "#ffffff", 0,
                    "Smallest planet and closest to the Sun; a cratered, airless world."),

                Planet("venus", "Venus", 6051.8, 4.8675e24, -5832.5, 177.4, 1.35, "#e6c98f",
                    "venus_surface.jpg", null, null, 60,
                    0.72333566, 0, 0.00677672, -0.00004107, 3.39467605, -0.00078890,
                    181.97909950, 58517.81538729, 131.60246718, 0.00268329, 76.67984255, -0.27769418,
                    null, false, "#ffffff", 0,
                    "Thick sulfuric-acid clouds and a runaway greenhouse: the hottest planet."),

                Planet("earth", "Earth", 6371.0, 5.97237e24, 23.9345, 23.44, 1.45, "#4d7dff",
                    "earth_daymap.jpg", "earth_nightmap.jpg", "earth_clouds.jpg", 90,
                    1.00000261, 0.00000562, 0.01671123, -0.00004392, -0.00001531, -0.01294668,
                    100.46457166, 35999.37244981, 102.93768193, 0.32327364, 0, 0,
                    new AtmosphereInfo { Color = "#7ab7ff", Intensity = 0.85, Power = 3.2 },
                    true, "#7ab7ff", 0.35,
                    "Our home: liquid-water oceans, an oxygen atmosphere, and life."),

                Moon(),
                Planet("mars", "Mars", 3389.5, 6.4171e23, 24.6229, 25.19, 1.05, "#d1683c",
                    "mars.jpg", null, null, 150,
                    1.52371034, 0.00001847, 0.09339410, 0.00007882, 1.84969142, -0.00813131,
                    -4.55343205, 19140.30268499, -23.94362959, 0.44441088, 49.55953891, -0.29257343,
                    new AtmosphereInfo { Color = "#e08a5f", Intensity = 0.25, Power = 4.0 },
                    false, "#ffffff", 0,
                    "The Red Planet: iron-oxide dust, polar caps, and ancient riverbeds."),

                Planet("jupiter", "Jupiter", 69911.0, 1.89813e27, 9.925, 3.13, 7.2, "#d8a074",
                    "jupiter.jpg", null, null, 800,
                    5.20288700, -0.00011607, 0.04838624, -0.00013253, 1.30439695, -0.00183714,
                    34.39644051, 3034.74612775, 14.72847983, 0.21252668, 100.47390909, 0.20469106,
                    new AtmosphereInfo { Color = "#d8b088", Intensity = 0.30, Power = 3.6 },
                    false, "#ffffff", 0,
                    "The giant of the Solar System; Great Red Spot and dozens of moons."),

                Planet("saturn", "Saturn", 58232.0, 5.68319e26, 10.656, 26.73, 6.6, "#e5d3a3",
                    "saturn.jpg", null, null, 1200,
                    9.53667594, -0.00125060, 0.05386179, -0.00050991, 2.48599187, 0.00193609,
                    49.95424423, 1222.49362201, 92.59887831, -0.41897216, 113.66242448, -0.28867794,
                    new AtmosphereInfo { Color = "#e5d3a3", Intensity = 0.22, Power = 4.0 },
                    false, "#ffffff", 0,
                    "The ringed jewel of the Solar System; rings of ice and rock."),

                Planet("uranus", "Uranus", 25362.0, 8.68103e25, -17.24, 97.77, 3.6, "#a8e0f0",
                    "uranus.jpg", null, null, 2500,
                    19.18916464, -0.00196176, 0.04725744, -0.00004397, 0.77263783, -0.00242939,
                    313.23810451, 428.48202785, 170.95427630, 0.40805281, 74.01692512, 0.04240589,
                    new AtmosphereInfo { Color = "#a8e0f0", Intensity = 0.28, Power = 4.0 },
                    false, "#ffffff", 0,
                    "An ice giant rolled on its side, likely from an ancient collision."),

                Planet("neptune", "Neptune", 24622.0, 1.0241e26, 16.11, 28.32, 3.5, "#5d7bff",
                    "neptune.jpg", null, null, 4000,
                    30.06992276, 0.00026291, 0.00859048, 0.00005105, 1.77004347, 0.00035372,
                    -55.12002969, 218.45945325, 44.96476227, -0.32241464, 131.78422574, -0.00508664,
                    new AtmosphereInfo { Color = "#5d7bff", Intensity = 0.35, Power = 3.6 },
                    false, "#ffffff", 0,
                    "The windiest planet; supersonic storms in a cold blue atmosphere."),

                Planet("pluto", "Pluto", 1188.3, 1.303e22, 153.29, 122.5, 0.38, "#cbb69b",
                    null, null, null, 8000,
                    39.48211675, -0.00031596, 0.24882730, 0.00005170, 17.14001206, 0.00004818,
                    238.92903833, 145.20780515, 224.06891629, -0.04062942, 110.30393684, -0.01183482,
                    null, false, "#ffffff", 0,
                    "The icy dwarf planet of the Kuiper Belt, with its heart-shaped glacier."),
            };

            var saturn = list.Find(b => b.Id == "saturn");
            if (saturn != null)
            {
                saturn.RingTexture = "saturn_ring_alpha.png";
                saturn.Flattening = 0.09796;
                saturn.RingColor = "#ddd1b4";
                saturn.RingInner = 1.24; // 1.11/0.9 visual radii ≈ 74,500/60,000 km
                saturn.RingOuter = 2.27; // 2.04/0.9 ≈ 136,800 km
            }

            var venus = list.Find(b => b.Id == "venus");
            if (venus != null)
            {
                // The base map is Magellan radar topography. An opaque, warm cloud
                // layer produces Venus's visible-light appearance.
                venus.CloudsTexture = "earth_clouds.jpg";
                venus.CloudsColor = "#f3d49a";
                venus.CloudsOpacity = 0.88;
                venus.Atmosphere = new AtmosphereInfo { Color = "#e8b96f", Intensity = 0.46, Power = 3.4 };
            }

            var earth = list.Find(b => b.Id == "earth");
            if (earth != null) earth.Flattening = 0.00335;
            var jupiter = list.Find(b => b.Id == "jupiter");
            if (jupiter != null) jupiter.Flattening = 0.06487;
            var uranus = list.Find(b => b.Id == "uranus");
            if (uranus != null)
            {
                uranus.Flattening = 0.02293;
                uranus.RingInner = 1.64;
                uranus.RingOuter = 2.00;
                uranus.RingColor = "#8aa0a4";
                uranus.RingOpacity = 0.34;
            }
            var neptune = list.Find(b => b.Id == "neptune");
            if (neptune != null) neptune.Flattening = 0.01708;
            var pluto = list.Find(b => b.Id == "pluto");
            if (pluto != null) pluto.Kind = "dwarf";

            list.AddRange(MajorMoons());
            list.AddRange(SmallBodySeed());
            ValidateCatalog(list);
            return list;
        }

        private static void ValidateCatalog(IReadOnlyList<BodyInfo> bodies)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var planetCount = 0;
            var moonCount = 0;
            foreach (var body in bodies)
            {
                if (string.IsNullOrWhiteSpace(body.Id) || !ids.Add(body.Id))
                    throw new InvalidOperationException($"Invalid or duplicate body id: '{body.Id}'.");
                if (body.Id != "sun" && !ids.Contains(body.Parent))
                    throw new InvalidOperationException($"Body '{body.Id}' appears before missing parent '{body.Parent}'.");
                if (body.RadiusKm <= 0 || body.VisualRadius <= 0)
                    throw new InvalidOperationException($"Body '{body.Id}' has an invalid radius.");
                if (body.Elements is { } el && (el.A <= 0 || el.E < 0 || el.E >= 1))
                    throw new InvalidOperationException($"Body '{body.Id}' has invalid elliptic elements.");
                if (body.Kind == "planet") planetCount++;
                if (body.Kind == "moon") moonCount++;
            }
            if (planetCount != 8 || moonCount < 20)
                throw new InvalidOperationException($"Catalog is incomplete ({planetCount} planets, {moonCount} moons).");
        }

        private static BodyInfo Star()
        {
            var sun = Base("sun", "Sun", "star", 696340.0, 1.98892e30, 609.12, 7.25, 13.0, "#ffcf5e");
            sun.Texture = "sun.jpg";
            sun.TrailDays = 0;
            sun.Description = "A G-type main-sequence star holding 99.8% of the Solar System's mass.";
            return sun;
        }

        private static BodyInfo Moon()
        {
            var moon = Base("moon", "Moon", "moon", 1737.4, 7.342e22, 655.728, 6.68, 0.38, "#cfcfcf");
            moon.Parent = "earth";
            moon.Texture = "moon.jpg";
            moon.OrbitScale = 3000;
            moon.TrailDays = 15;
            moon.Elements = new OrbitalElements
            {
                EpochDays = 0,
                A = 384400.0 / AuKm, Adot = 0,
                E = 0.0554, Edot = 0,
                I = 5.16, Idot = 0,
                Raan = 125.08, Raandot = 0,
                Wp = 318.15, Wpdot = 0,
                M0 = 135.27, Mdot = 360.0 / 27.322,
            };
            moon.Description = "Earth's only natural satellite, tidally locked and cratered.";
            return moon;
        }

        /// <summary>
        /// Major natural satellites using JPL mean elements at J2000. The giant-planet
        /// and Pluto-system elements are expressed in their parent's equatorial/Laplace
        /// plane; the renderer rotates those local planes with the parent body's axis.
        /// </summary>
        private static List<BodyInfo> MajorMoons() => new()
        {
            Satellite("phobos", "Phobos", "mars", 11.08, 1.0659e16, 0.3187, 9375, 0.015, 216.3, 189.7, 1.1, 169.2, 21000, 0.14, "#8f8171", "Mars's larger, innermost moon."),
            Satellite("deimos", "Deimos", "mars", 6.2, 1.4762e15, 1.2625, 23457, 0.000, 0.0, 205.0, 1.8, 54.3, 21000, 0.12, "#a89b89", "Mars's small outer moon."),

            Satellite("io", "Io", "jupiter", 1821.49, 8.9319e22, 1.762732, 421800, 0.004, 49.1, 330.9, 0.0, 0.0, 6000, 0.42, "#d9bd58", "Volcanically active Galilean moon of Jupiter."),
            Satellite("europa", "Europa", "jupiter", 1560.80, 4.7998e22, 3.525463, 671100, 0.009, 45.0, 345.4, 0.5, 184.0, 6000, 0.38, "#c9b58c", "Ice-covered Galilean moon with a global subsurface ocean."),
            Satellite("ganymede", "Ganymede", "jupiter", 2631.20, 1.4819e23, 7.155588, 1070400, 0.001, 198.3, 324.8, 0.2, 58.5, 6000, 0.50, "#918675", "The Solar System's largest moon."),
            Satellite("callisto", "Callisto", "jupiter", 2410.30, 1.0759e23, 16.690440, 1882700, 0.007, 43.8, 87.4, 0.3, 309.1, 6000, 0.47, "#746b61", "A dark, ancient and heavily cratered Galilean moon."),

            Satellite("mimas", "Mimas", "saturn", 198.20, 3.7493e19, 0.942422, 186000, 0.020, 160.4, 275.3, 1.6, 66.2, 5500, 0.20, "#c7c4bd", "Small icy moon marked by the giant Herschel crater."),
            Satellite("enceladus", "Enceladus", "saturn", 252.10, 1.0802e20, 1.370218, 238400, 0.005, 119.5, 57.0, 0.0, 0.0, 5500, 0.23, "#e5ecf0", "Bright icy moon with an active ocean and south-polar plumes."),
            Satellite("tethys", "Tethys", "saturn", 531.10, 6.1745e20, 1.887802, 295000, 0.001, 335.3, 0.0, 1.1, 273.0, 5500, 0.28, "#d8d5ce", "Icy Saturnian moon with the vast Odysseus crater."),
            Satellite("dione", "Dione", "saturn", 561.40, 1.0955e21, 2.736916, 377700, 0.002, 116.0, 212.0, 0.0, 0.0, 5500, 0.29, "#c9c8c4", "Icy moon with bright fractured terrain."),
            Satellite("rhea", "Rhea", "saturn", 763.50, 2.3065e21, 4.517503, 527200, 0.001, 44.3, 31.5, 0.3, 133.7, 5500, 0.33, "#c4c1ba", "Saturn's second-largest moon."),
            Satellite("titan", "Titan", "saturn", 2574.76, 1.3452e23, 15.945448, 1221900, 0.029, 78.3, 11.7, 0.3, 78.6, 5500, 0.52, "#d5a34f", "A large moon with a dense nitrogen atmosphere and methane seas.", true),
            Satellite("iapetus", "Iapetus", "saturn", 734.30, 1.8056e21, 79.331002, 3561700, 0.028, 254.5, 74.8, 7.6, 86.5, 5500, 0.32, "#9b927f", "Two-toned outer moon with a prominent equatorial ridge."),

            Satellite("miranda", "Miranda", "uranus", 235.8, 6.59e19, 1.413479, 129846, 0.001, 154.8, 73.0, 4.4, 100.9, 5000, 0.21, "#c8c5be", "Small Uranian moon with dramatic fault canyons."),
            Satellite("ariel", "Ariel", "uranus", 578.9, 1.353e21, 2.520379, 190929, 0.001, 9.6, 193.5, 0.0, 0.0, 5000, 0.29, "#d7d5d0", "Bright, geologically varied moon of Uranus."),
            Satellite("umbriel", "Umbriel", "uranus", 584.7, 1.172e21, 4.144177, 265986, 0.004, 183.4, 253.0, 0.1, 174.8, 5000, 0.29, "#77746f", "The darkest of Uranus's five major moons."),
            Satellite("titania", "Titania", "uranus", 788.9, 3.527e21, 8.705869, 436298, 0.002, 184.0, 68.1, 0.1, 29.5, 5000, 0.34, "#aaa7a0", "The largest moon of Uranus."),
            Satellite("oberon", "Oberon", "uranus", 761.4, 3.014e21, 13.463237, 583511, 0.002, 132.2, 143.6, 0.1, 76.8, 5000, 0.33, "#8e877d", "A dark, cratered outer major moon of Uranus."),

            Satellite("triton", "Triton", "neptune", 1352.60, 2.139e22, 5.876994, 354800, 0.000, 0.0, 63.0, 157.3, 178.1, 5000, 0.40, "#c9b8ad", "Neptune's large retrograde moon with active nitrogen geysers."),
            Satellite("charon", "Charon", "pluto", 606.0, 1.586e21, 6.387222, 19600, 0.000, 0.0, 304.1, 0.0, 0.0, 5000, 0.28, "#a6a09a", "Pluto's large companion; the pair orbit a barycenter outside Pluto."),
        };

        private static BodyInfo Satellite(
            string id, string name, string parent, double radiusKm, double massKg,
            double periodDays, double aKm, double e, double wp, double m0,
            double i, double raan, double orbitScale, double visualRadius,
            string color, string description, bool atmosphere = false)
        {
            var moon = Base(id, name, "moon", radiusKm, massKg, periodDays * 24.0, 0, visualRadius, color);
            moon.Parent = parent;
            moon.OrbitScale = orbitScale;
            moon.OrbitInParentEquator = true;
            moon.TrailDays = Math.Min(periodDays, 30.0);
            moon.Description = description;
            moon.Elements = new OrbitalElements
            {
                EpochDays = 0,
                A = aKm / AuKm,
                E = e,
                I = i,
                Raan = raan,
                Wp = wp,
                M0 = m0,
                Mdot = 360.0 / periodDays,
            };
            if (atmosphere)
                moon.Atmosphere = new AtmosphereInfo { Color = "#d5a34f", Intensity = 0.28, Power = 3.8 };
            return moon;
        }

        private static BodyInfo Planet(
            string id, string name, double rKm, double massKg, double rotH, double tilt,
            double visualR, string color,
            string? texture, string? nightTexture, string? cloudsTexture, double trailDays,
            double a0, double da, double e0, double de, double i0, double di,
            double L0, double dL, double wbar0, double dwbar, double node0, double dnode,
            AtmosphereInfo? atmo, bool specular, string specColor, double specIntensity,
            string description)
        {
            var b = Base(id, name, "planet", rKm, massKg, rotH, tilt, visualR, color);
            b.Texture = texture;
            b.NightTexture = nightTexture;
            b.CloudsTexture = cloudsTexture;
            b.TrailDays = trailDays;
            b.Atmosphere = atmo;
            b.Specular = specular;
            b.SpecColor = specColor;
            b.SpecIntensity = specIntensity;
            b.Description = description;
            b.Elements = ElementsFromLongitudes(a0, da, e0, de, i0, di, L0, dL, wbar0, dwbar, node0, dnode);
            return b;
        }

        /// <summary>
        /// Builds canonical elements from JPL mean longitudes (L, ϖ, Ω at J2000, rates per century).
        /// </summary>
        private static OrbitalElements ElementsFromLongitudes(
            double a0, double da, double e0, double de, double i0, double di,
            double L0, double dL, double wbar0, double dwbar, double node0, double dnode)
        {
            return new OrbitalElements
            {
                EpochDays = 0,
                A = a0, Adot = da / DaysPerCentury,
                E = e0, Edot = de / DaysPerCentury,
                I = i0, Idot = di / DaysPerCentury,
                Raan = node0, Raandot = dnode / DaysPerCentury,
                Wp = wbar0 - node0, Wpdot = (dwbar - dnode) / DaysPerCentury,
                M0 = L0 - wbar0, Mdot = dL / DaysPerCentury,
            };
        }

        private static BodyInfo Base(string id, string name, string kind, double rKm, double massKg,
            double rotH, double tilt, double visualR, string color) =>
            new BodyInfo
            {
                Id = id,
                Name = name,
                Kind = kind,
                Parent = "sun",
                RadiusKm = rKm,
                MassKg = massKg,
                RotationHours = rotH,
                AxialTiltDeg = tilt,
                VisualRadius = visualR,
                Color = color,
            };

        /// <summary>
        /// Seed of major small bodies so the app always has asteroid/comet content offline.
        /// Elements are real JPL osculating values (a, e, i, Ω, ω in deg; tp = perihelion
        /// time in JD; n = mean motion in deg/day); M0 is derived from tp so positions are
        /// accurate from any epoch. The catalog is refreshed from JPL SBDB at startup.
        /// </summary>
        private static List<BodyInfo> SmallBodySeed()
        {
            var list = new List<BodyInfo>();
            var seeds = new (string id, string name, string kind, string sClass, string color,
                double a, double e, double i, double raan, double wp, double tp, double n, double dKm)[]
            {
                ("ceres", "Ceres", "asteroid", "MBA", "#b9b4ab", 2.77, 0.0797, 10.59, 80.26, 73.34, 2461599.841, 0.21416, 939.4),
                ("vesta", "Vesta", "asteroid", "MBA", "#c8bfb4", 2.36, 0.0902, 7.14, 103.7, 151.7, 2460901.587, 0.27158, 525.4),
                ("pallas", "Pallas", "asteroid", "MBA", "#d6d0c2", 2.77, 0.231, 34.9, 173.1, 310.9, 2461695.031, 0.21406, 512.0),
                ("hygiea", "Hygiea", "asteroid", "MBA", "#b0afa8", 3.14, 0.109, 3.83, 283.2, 312.2, 2461813.201, 0.17772, 433.0),
                ("eros", "Eros", "asteroid", "AMO", "#d8c8a0", 1.46, 0.223, 10.8, 304.3, 178.9, 2461088.813, 0.55993, 16.84),
                ("bennu", "Bennu", "asteroid", "APO", "#9b8f7a", 1.13, 0.204, 6.03, 2.06, 66.2, 2455439.142, 0.82655, 0.49),
                ("apophis", "Apophis", "asteroid", "ATE", "#b0a49a", 0.922, 0.191, 3.34, 204.4, 126.4, 2461042.919, 1.11022, 0.37),
                ("didymos", "Didymos", "asteroid", "APO", "#a89a8a", 1.64, 0.383, 3.41, 73.1, 320.3, 2461412.278, 0.46873, 0.78),
                ("2024-pt5", "2024 PT5", "asteroid", "ATE", "#a49a90", 1.02, 0.0153, 1.71, 289.1, 196.6, 2461077.557, 0.95380, 0.011),
                ("ryugu", "Ryugu", "asteroid", "APO", "#7a7066", 1.19, 0.191, 5.87, 251.6, 211.6, 2461118.296, 0.75878, 0.87),
                ("itokawa", "Itokawa", "asteroid", "APO", "#8a8378", 1.32, 0.28, 1.62, 69.1, 163.2, 2460936.703, 0.64747, 0.35),
                ("1p-halley", "1P/Halley", "comet", "HTC", "#bfe8ff", 17.9, 0.968, 162.3, 59.1, 112.0, 2446469.974, 0.013030, 11.0),
                ("67p-churyumov-gerasimenko", "67P/Churyumov–Gerasimenko", "comet", "JFc", "#bfe8ff", 3.46, 0.641, 7.04, 50.1, 12.8, 2457247.589, 0.153200, 4.0),
            };
            foreach (var s in seeds)
            {
                var b = Base(s.id, s.name, s.kind, s.dKm / 2.0, 0, 0, 0,
                    Math.Clamp(s.dKm * 0.0010, 0.08, 1.0), s.color);
                b.DiameterKm = s.dKm;
                b.SbdbClass = s.sClass;
                b.Elements = new OrbitalElements
                {
                    EpochDays = 0, // M0 below is exact for epoch J2000 via tp
                    A = s.a, Adot = 0, E = Math.Min(s.e, 0.9999), Edot = 0,
                    I = s.i, Idot = 0, Raan = s.raan, Raandot = 0,
                    Wp = s.wp, Wpdot = 0, M0 = NormDeg(s.n * (2451545.0 - s.tp)), Mdot = s.n,
                };
                b.TrailDays = Math.Min(2 * 365.2569 * Math.Pow(s.a, 1.5), 3000);
                b.Description = $"JPL Small-Body Database object ({s.sClass})." +
                                (s.dKm > 0 ? $" Diameter ≈ {s.dKm:F1} km." : "");
                list.Add(b);
            }
            return list;
        }

        private static double NormDeg(double deg)
        {
            deg %= 360.0;
            return deg < 0 ? deg + 360.0 : deg;
        }
    }
}
