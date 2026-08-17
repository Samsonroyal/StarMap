namespace StarMap.Models
{
    public sealed class AtmosphereInfo
    {
        public string? Color { get; set; }      // hex string
        public double Intensity { get; set; }   // rim glow strength
        public double Power { get; set; }       // fresnel power
    }

    /// <summary>
    /// A celestial body. <see cref="Elements"/> are heliocentric for sun-parented
    /// bodies and geocentric for the Moon. Serialized to the web renderer as JSON.
    /// </summary>
    public sealed class BodyInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Kind { get; set; } = "planet"; // star | planet | dwarf | moon | asteroid | comet
        public string Parent { get; set; } = "sun";

        public double RadiusKm { get; set; }
        public double MassKg { get; set; }
        public double RotationHours { get; set; }
        public double RotationPhaseDeg { get; set; } // prime-meridian angle at J2000
        public double AxialTiltDeg { get; set; }
        public double Flattening { get; set; }       // (equatorial - polar) / equatorial

        // Visual parameters (scene units, not physical scale)
        public double VisualRadius { get; set; }
        public string? Color { get; set; }

        public string? Texture { get; set; }
        public string? NightTexture { get; set; }
        public string? CloudsTexture { get; set; }
        public string CloudsColor { get; set; } = "#ffffff";
        public double CloudsOpacity { get; set; } = 0.65;
        public string? RingTexture { get; set; }
        public double RingInner { get; set; }   // in multiples of VisualRadius
        public double RingOuter { get; set; }
        public string RingColor { get; set; } = "#ffffff";
        public double RingOpacity { get; set; } = 1.0;

        public AtmosphereInfo? Atmosphere { get; set; }
        public bool Specular { get; set; }
        public string SpecColor { get; set; } = "#ffffff";
        public double SpecIntensity { get; set; }

        public OrbitalElements? Elements { get; set; }
        // Solar orbits use 60 scene units/AU. Satellite systems deliberately use a
        // larger, internally consistent display scale so their real orbital hierarchy
        // remains readable beside visually exaggerated planets.
        public double OrbitScale { get; set; } = 60.0;
        public bool OrbitInParentEquator { get; set; }
        public double TrailDays { get; set; }

        // Small-body metadata
        public double? DiameterKm { get; set; }
        public string? SbdbClass { get; set; }
        public string? Description { get; set; }
    }
}
