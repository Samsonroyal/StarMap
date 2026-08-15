using System;

namespace StarMap.Models
{
    /// <summary>
    /// Canonical Keplerian elements. All angles in degrees; time in days since J2000
    /// (JD 2451545.0). Rate fields are per-day for planets whose elements drift.
    /// </summary>
    public sealed class OrbitalElements
    {
        public double EpochDays { get; set; }   // days from J2000 epoch of osculation

        public double A { get; set; }           // semi-major axis, AU
        public double Adot { get; set; }        // AU/day
        public double E { get; set; }           // eccentricity
        public double Edot { get; set; }        // /day
        public double I { get; set; }           // inclination, deg
        public double Idot { get; set; }        // deg/day
        public double Raan { get; set; }        // longitude of ascending node, deg
        public double Raandot { get; set; }     // deg/day
        public double Wp { get; set; }          // argument of periapsis, deg
        public double Wpdot { get; set; }       // deg/day
        public double M0 { get; set; }          // mean anomaly at epoch, deg
        public double Mdot { get; set; }        // mean motion, deg/day

        public double PeriodDays => 365.256898326 * Math.Pow(A, 1.5);
    }
}
