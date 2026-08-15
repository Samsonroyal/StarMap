using System;
using StarMap.Models;

namespace StarMap.Data
{
    /// <summary>
    /// Keplerian propagation in AU / degrees. Mirrors Assets/web/src/ephemeris.js so
    /// the native shell can compute positions for the inspector without a web round-trip.
    /// </summary>
    public static class Ephemeris
    {
        public const double J2000JD = 2451545.0;
        public const double AU_KM = 149597870.7;

        private const double DegToRad = Math.PI / 180.0;

        /// <summary>Julian Date from a UTC DateTime.</summary>
        public static double JulianFromUtc(DateTime utc) =>
            utc.ToUniversalTime().Ticks / TimeSpan.TicksPerDay + 2440587.5;

        /// <summary>Days since J2000.</summary>
        public static double DaysSinceJ2000(DateTime utc) => JulianFromUtc(utc) - J2000JD;

        /// <summary>
        /// Position (AU) of a body relative to its parent, and its distance from parent.
        /// Returns NaN for bodies with no elements (the Sun).
        /// </summary>
        public static (double X, double Y, double Z, double R) PositionRelative(OrbitalElements el, double tDays)
        {
            var dt = tDays - el.EpochDays;
            var a = el.A + el.Adot * dt;
            var e = Math.Min(0.999999, el.E + el.Edot * dt);
            var i = (el.I + el.Idot * dt) * DegToRad;
            var raan = (el.Raan + el.Raandot * dt) * DegToRad;
            var wp = (el.Wp + el.Wpdot * dt) * DegToRad;
            var m = (el.M0 + el.Mdot * dt) * DegToRad;

            var eAnom = SolveKepler(m, e);
            var nu = 2.0 * Math.Atan2(Math.Sqrt(1 + e) * Math.Sin(eAnom / 2), Math.Sqrt(1 - e) * Math.Cos(eAnom / 2));
            var r = a * (1 - e * Math.Cos(eAnom));

            var x = r * Math.Cos(nu);
            var y = r * Math.Sin(nu);

            var cr = Math.Cos(raan); var sr = Math.Sin(raan);
            var cw = Math.Cos(wp); var sw = Math.Sin(wp);
            var ci = Math.Cos(i); var si = Math.Sin(i);

            var X = (cr * cw - sr * sw * ci) * x + (-cr * sw - sr * cw * ci) * y;
            var Y = (sr * cw + cr * sw * ci) * x + (-sr * sw + cr * cw * ci) * y;
            var Z = (sw * si) * x + (cw * si) * y;

            return (X, Y, Z, r);
        }

        /// <summary>
        /// Heliocentric position (AU) honoring parent chains (e.g., Earth for the Moon).
        /// </summary>
        public static (double X, double Y, double Z, double R) Heliocentric(BodyInfo body, DateTime utc, Func<string, BodyInfo?> lookup)
        {
            var t = DaysSinceJ2000(utc);
            return Heliocentric(body, t, lookup);
        }

        public static (double X, double Y, double Z, double R) Heliocentric(BodyInfo body, double tDays, Func<string, BodyInfo?> lookup)
        {
            if (body.Elements == null || string.Equals(body.Parent, "sun", StringComparison.OrdinalIgnoreCase))
            {
                if (body.Elements == null)
                    return (0, 0, 0, 0);
                return PositionRelative(body.Elements, tDays);
            }

            var parent = lookup(body.Parent);
            if (parent == null)
                return PositionRelative(body.Elements, tDays);

            var (px, py, pz, _) = Heliocentric(parent, tDays, lookup);
            var (rx, ry, rz, rr) = PositionRelative(body.Elements, tDays);
            return (px + rx, py + ry, pz + rz, rr);
        }

        /// <summary>Newton–Raphson solve of Kepler's equation M = E − e·sin(E).</summary>
        public static double SolveKepler(double m, double e)
        {
            m = NormalizeAngle(m);
            var eAnom = e < 0.8 ? m : Math.PI;
            for (var i = 0; i < 40; i++)
            {
                var f = eAnom - e * Math.Sin(eAnom) - m;
                var fp = 1.0 - e * Math.Cos(eAnom);
                var d = f / fp;
                eAnom -= d;
                if (Math.Abs(d) < 1e-10) break;
            }
            return eAnom;
        }

        public static double NormalizeAngle(double rad)
        {
            while (rad > Math.PI) rad -= 2 * Math.PI;
            while (rad < -Math.PI) rad += 2 * Math.PI;
            return rad;
        }
    }
}
