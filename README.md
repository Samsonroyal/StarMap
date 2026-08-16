# StarMap

A Windows desktop recreation of NASA's "Eyes on the Solar System": a native WinUI 3 (Windows App SDK) shell hosting a Three.js WebView2 scene driven by real JPL ephemeris data, with an immersive overlay interface, time controls, layer toggles, and a contextual inspector.

## Features

- **Live Solar System** — Sun, 8 planets, the Moon, Pluto, and 13 JPL small bodies (Ceres, Vesta, Pallas, Hygiea, Eros, Bennu, Apophis, Didymos, 2024 PT5, Ryugu, Itokawa, 1P/Halley, 67P).
- **Real ephemeris** — planets use JPL "Approximate Positions" mean elements; small bodies use osculating elements fetched from the [JPL SBDB API](https://ssd-api.jpl.nasa.gov/) and cached locally (`%LocalAppData%\StarMap\sbdb-cache.json`) so the app works offline. Kepler's equation is solved identically in C# and JS.
- **Rendering** — Three.js scene with:
  - Textured planets (Solar System Scope 2K maps), Earth day/night shader with specular oceans, cloud layer, and atmosphere rim-glow.
  - Saturn's rings with radial UV-mapped alpha texture; axial tilt applied to each globe.
  - Sun glow sprite, starfield, decorative asteroid belt.
  - Orbit rings, animated trails, CSS2D labels.
  - Raycast picking: hover highlights a label, click selects in the inspector, double-click focuses the camera.
- **Time controls** — play/pause, 10 speed presets (1 s/s … 1000 yr/s), date/time pickers, "Now" button. The inspector shows live distance, semi-major axis, eccentricity, inclination, and period.
- **Layer toggles** — Planets, Moons, Orbits, Trails, Labels, Stars, Asteroid belt, Small bodies.
- **Searchable body list** and a contextual left-side inspector panel.
- **Eyes-style navigation** — edge-to-edge space viewport, Explore/Info/View overlays, breadcrumb selection, camera rail, overview reset, zoom controls, natural/flood lighting, and a centered live-time console.
- **Desktop interactions** — native tooltips and dialogs, clipboard sharing, plus Space, Home, Ctrl+F, and Escape keyboard shortcuts.
- Offline-first: a small-body seed catalog with real elements is bundled, and the SBDB cache is reused on startup.

## Native UI boundary

All application UI is native WinUI 3: `Window`, `Grid`, `Border`, `Button`, `ToggleButton`, `ToggleSwitch`, `TextBox`, `ListView`, `DatePicker`, `TimePicker`, `ComboBox`, `ContentDialog`, and related XAML primitives come from `Microsoft.UI.Xaml`. WebView2 is used only as the GPU-backed surface for the Three.js solar-system visualization; it does not implement the app chrome, panels, forms, menus, or accessibility semantics.

The current interface needs no Windows Community Toolkit dependency because WinUI 3 already supplies every control used here. If a Toolkit component is introduced later, use the `CommunityToolkit.WinUI.*` package family intended for Windows App SDK / WinUI 3—not the UWP `CommunityToolkit.Uwp.*` packages.

## Building

Prerequisites: .NET 8 SDK, Windows App SDK / WinUI 3 workload, WebView2 runtime.

```
dotnet build -c Debug -p:Platform=x64
```

Run the resulting `bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\StarMap.exe`, or open the `.csproj` in Visual Studio and press F5.

The app is unpackaged and self-contained (`WindowsPackageType=None`, `WindowsAppSDKSelfContained=true`) so it runs without MSIX signing or a runtime installer.

## Project layout

- `MainWindow.xaml(.cs)` — native WinUI shell: title bar, contextual overlays, camera rail, search, inspector, layer options, time console, and host↔web messaging bridge.
- `Data/BodyCatalog.cs` — bundled catalog (planets, Moon, Pluto, small-body seed).
- `Data/Ephemeris.cs` — Keplerian propagation mirror (C# side).
- `SbdbClient.cs` — JPL SBDB client + cache.
- `Models/` — `BodyInfo`, `OrbitalElements`, `AtmosphereInfo`.
- `Assets/web/` — the embedded Three.js renderer (no CDN, fully offline):
  - `index.html` — import map + label/splash styling.
  - `js/` — vendored `three.module.js`, `OrbitControls.js`, `CSS2DRenderer.js`.
  - `textures/` — Solar System Scope maps.
  - `src/ephemeris.js`, `scene.js`, `app.js` — Kepler math, 3D scene, host messaging.

## Notes

- Log: `%LocalAppData%\StarMap\starmap.log` (startup, web bridge, SBDB fetch results).
- Positions use mean osculating elements — accurate for visualization, not mission-grade; for that, integrate NAIF SPICE kernels.
