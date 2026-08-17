using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Core;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using StarMap.Data;
using StarMap.Models;

namespace StarMap
{
    /// <summary>A body list row bound to the left rail ListView.</summary>
    public sealed class BodyRow
    {
        public string Id { get; }
        public string Name { get; }
        public string Subtitle { get; }
        public string Icon { get; }

        public BodyRow(BodyInfo body)
        {
            Id = body.Id;
            Name = body.Name;
            Subtitle = body.Kind switch
            {
                "star" => "Star",
                "planet" => "Planet",
                "dwarf" => "Dwarf planet",
                "moon" => "Moon of " + body.Parent,
                "asteroid" => "Asteroid" + (body.SbdbClass != null ? $" · {body.SbdbClass}" : ""),
                "comet" => "Comet",
                _ => body.Kind,
            };
            Icon = body.Kind == "star" ? "\uE706" : "\uE707";
        }
    }

    /// <summary>An inspector label/value row.</summary>
    public sealed class InspectorRow : INotifyPropertyChanged
    {
        public string Label { get; set; } = "";
        private string _value = "";
        public string Value
        {
            get => _value;
            set
            {
                if (_value == value) return;
                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>A speed preset (seconds of simulation per real second).</summary>
    public sealed class SpeedPreset
    {
        public string Label { get; }
        public double Seconds { get; }
        public SpeedPreset(string label, double seconds) { Label = label; Seconds = seconds; }
    }

    public sealed partial class MainWindow : Window
    {
        private static readonly JsonSerializerOptions _jsonOpts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };

        private readonly List<BodyInfo> _allBodies = new();
        private readonly List<BodyInfo> _pendingSmallBodies = new();
        private readonly Dictionary<string, BodyRow> _rowsById = new(StringComparer.OrdinalIgnoreCase);

        private bool _webReady;
        private bool _firstFrameReceived;
        private bool _syncingTime;
        private bool _syncingSelection;
        private bool _playing = true;
        private double _speedSeconds = 1.0;
        private DateTime _simTimeUtc = DateTime.UtcNow;
        private BodyInfo? _selected;
        private InspectorRow? _distanceRow;
        private string? _pendingFocus;
        private bool _floodLighting;
        private readonly DispatcherTimer _toastTimer = new();

        public ObservableCollection<BodyRow> BodyRows { get; } = new();

        public MainWindow()
        {
            // WinUI 3 unpackaged apps don't install a SynchronizationContext by
            // default; without it async continuations resume on the thread pool and
            // touching UI collections throws 0x8001010E.
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherQueueSynchronizationContext(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()));

            this.InitializeComponent();
            App.Log.Info("MainWindow constructed.");

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(HeaderDragRegion);

            AppWindow.Resize(new Windows.Graphics.SizeInt32(1440, 900));

            _toastTimer.Interval = TimeSpan.FromSeconds(2.4);
            _toastTimer.Tick += (_, _) =>
            {
                _toastTimer.Stop();
                ToastBar.Visibility = Visibility.Collapsed;
            };

            BuildSpeedPresets();
            BuildBodyList(BodyCatalog.All);

            DatePicker.MinYear = new DateTimeOffset(1600, 1, 1, 0, 0, 0, TimeSpan.Zero);
            DatePicker.MaxYear = new DateTimeOffset(2600, 12, 31, 0, 0, 0, TimeSpan.Zero);
            TimePicker.ClockIdentifier = "24HourClock";
            SyncTimeControls();

            _ = InitializeAsync();
            _ = LoadSmallBodiesAsync();
        }

        // ============================================================ WebView setup

        private async Task InitializeAsync()
        {
            try
            {
                await WebView.EnsureCoreWebView2Async();
                var wv = WebView.CoreWebView2;
                if (wv == null) return;

                wv.Settings.AreDefaultContextMenusEnabled = false;
                wv.Settings.AreDevToolsEnabled = false;
                wv.WebMessageReceived += OnWebMessage;
                wv.NavigationCompleted += OnNavigationCompleted;

                var webRoot = Path.Combine(AppContext.BaseDirectory, "Assets", "web");
                if (!Directory.Exists(webRoot))
                {
                    ShowError($"Web assets not found at {webRoot}");
                    return;
                }

                wv.SetVirtualHostNameToFolderMapping("starmap.local", webRoot, CoreWebView2HostResourceAccessKind.Allow);
                wv.Navigate("https://starmap.local/index.html");
            }
            catch (Exception ex)
            {
                App.Log.Error($"WebView init failed: {ex}");
                ShowError("Failed to initialize the 3D view: " + ex.Message);
            }
        }

        private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess)
            {
                ShowError($"The 3D view failed to load (error {e.WebErrorStatus}).");
            }
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorBar.Visibility = Visibility.Visible;
            LoadingRing.IsActive = false;
            LoadingOverlay.Visibility = Visibility.Collapsed;
            App.Log.Error(message);
        }

        // ============================================================ Messaging

        private bool TryPost(object payload)
        {
            if (WebView?.CoreWebView2 == null) return false;
            try
            {
                WebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload, _jsonOpts));
                return true;
            }
            catch (Exception ex)
            {
                App.Log.Error($"Post failed: {ex.Message}");
                return false;
            }
        }

        private void OnWebMessage(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            try
            {
                using var doc = JsonDocument.Parse(args.WebMessageAsJson);
                var root = doc.RootElement;
                if (!root.TryGetProperty("type", out var typeProp)) return;
                var type = typeProp.GetString();

                switch (type)
                {
                    case "ready":
                        OnWebReady();
                        break;
                    case "selected":
                        if (root.TryGetProperty("id", out var sel))
                        {
                            if (sel.ValueKind == JsonValueKind.String && sel.GetString() is string selId)
                                SelectBody(selId, focus: false);
                            else
                                ClearSelection();
                        }
                        break;
                    case "frame":
                        OnFrame(root);
                        break;
                    case "log":
                        if (root.TryGetProperty("level", out var lvl) && root.TryGetProperty("msg", out var msg))
                            App.Log.Info($"[web:{lvl.GetString()}] {msg.GetString()}");
                        break;
                }
            }
            catch (Exception ex)
            {
                App.Log.Error($"Web message parse failed: {ex.Message}");
            }
        }

        private void OnWebReady()
        {
            _webReady = true;
            App.Log.Info("Web renderer ready.");

            var payload = new
            {
                type = "init",
                timeIso = _simTimeUtc.ToString("o"),
                speedSeconds = _speedSeconds,
                playing = _playing,
                bodies = _allBodies,
            };
            TryPost(payload);
            PostToggles();
            TryPost(new { type = "setLighting", flood = _floodLighting });

            if (_pendingSmallBodies.Count > 0)
                PostAddBodies(_pendingSmallBodies);

            if (_pendingFocus != null)
            {
                TryPost(new { type = "focus", id = _pendingFocus });
                _pendingFocus = null;
            }
        }

        private void PostToggles() =>
            TryPost(new
            {
                type = "toggles",
                data = new
                {
                    planets = TogglePlanets.IsOn,
                    moons = ToggleMoons.IsOn,
                    orbits = ToggleOrbits.IsOn,
                    trails = ToggleTrails.IsOn,
                    labels = ToggleLabels.IsOn,
                    stars = ToggleStars.IsOn,
                    belt = ToggleBelt.IsOn,
                    smallBodies = ToggleSmallBodies.IsOn,
                },
            });

        private void PostAddBodies(IEnumerable<BodyInfo> bodies) =>
            TryPost(new { type = "addBodies", bodies });

        private void OnFrame(JsonElement root)
        {
            if (!_firstFrameReceived)
            {
                _firstFrameReceived = true;
                LoadingRing.IsActive = false;
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }

            if (root.TryGetProperty("timeIso", out var t) && t.ValueKind == JsonValueKind.String)
            {
                if (DateTime.TryParse(t.GetString(), CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
                    _simTimeUtc = parsed;
            }

            SimClockText.Text = _simTimeUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");

            if (root.TryGetProperty("fps", out var fps) && fps.ValueKind == JsonValueKind.Number)
                FpsText.Text = $"{fps.GetDouble():F0} fps";

            if (_selected?.Elements != null && _distanceRow != null)
                _distanceRow.Value = FormatDistance(
                    ComputeDistance(_selected),
                    !string.Equals(_selected.Parent, "sun", StringComparison.OrdinalIgnoreCase));
        }

        private double ComputeDistance(BodyInfo body)
        {
            if (body.Elements == null) return 0;
            var (_, _, _, distanceFromParent) = Ephemeris.PositionRelative(
                body.Elements, Ephemeris.DaysSinceJ2000(_simTimeUtc));
            return distanceFromParent;
        }

        // ============================================================ Catalog / list

        private void BuildBodyList(IEnumerable<BodyInfo> bodies)
        {
            foreach (var body in bodies)
            {
                _allBodies.Add(body);
                AddRow(body);
            }
        }

        private void AddRow(BodyInfo body)
        {
            if (_rowsById.ContainsKey(body.Id)) return;
            var row = new BodyRow(body);
            _rowsById[body.Id] = row;
            BodyRows.Add(row);
        }

        private async Task LoadSmallBodiesAsync()
        {
            try
            {
                App.Log.Info("Small body fetch starting…");
                var smallBodies = await SbdbClient.FetchSmallBodiesAsync();
                App.Log.Info($"Small body fetch done: {smallBodies.Count} bodies.");
                foreach (var body in smallBodies)
                {
                    if (!_rowsById.ContainsKey(body.Id))
                    {
                        _pendingSmallBodies.Add(body);
                        AddRow(body);
                    }
                }

                if (_webReady && _pendingSmallBodies.Count > 0)
                    PostAddBodies(_pendingSmallBodies);
            }
            catch (Exception ex)
            {
                App.Log.Error($"Small body load failed: {ex}");
            }
        }

        // ============================================================ Selection / inspector

        private void SelectBody(string id, bool focus)
        {
            var body = _allBodies.Find(b => string.Equals(b.Id, id, StringComparison.OrdinalIgnoreCase));
            if (body == null) return;

            _selected = body;
            ShowInspector(body);

            _syncingSelection = true;
            if (_rowsById.TryGetValue(body.Id, out var row))
                BodiesList.SelectedItem = row;
            _syncingSelection = false;

            if (focus)
            {
                if (_webReady)
                    TryPost(new { type = "focus", id = body.Id });
                else
                    _pendingFocus = body.Id;
            }
        }

        private void ShowInspector(BodyInfo body)
        {
            InspectorEmpty.Visibility = Visibility.Collapsed;
            InspectorBody.Visibility = Visibility.Visible;

            InspectorName.Text = body.Name;
            InspectorKind.Text = body.Kind switch
            {
                "star" => "Star",
                "planet" => "Planet",
                "dwarf" => "Dwarf planet",
                "moon" => $"Moon of {Capitalize(body.Parent)}",
                "asteroid" => $"Asteroid{(body.SbdbClass != null ? $" · {body.SbdbClass}" : "")}",
                "comet" => "Comet",
                _ => body.Kind,
            };

            var rows = new List<InspectorRow>();

            if (body.DiameterKm is double d && body.Kind is "asteroid" or "comet")
                rows.Add(Row("Diameter", $"{d:N0} km"));

            if (body.RadiusKm > 0)
                rows.Add(Row("Radius", $"{body.RadiusKm:N0} km"));

            if (body.MassKg > 0)
                rows.Add(Row("Mass", FormatMass(body.MassKg)));

            _distanceRow = null;
            if (body.Elements != null)
            {
                var parent = string.Equals(body.Parent, "sun", StringComparison.OrdinalIgnoreCase) ? "Sun" : Capitalize(body.Parent);
                var isSatellite = !string.Equals(body.Parent, "sun", StringComparison.OrdinalIgnoreCase);
                _distanceRow = Row($"Distance from {parent}", FormatDistance(ComputeDistance(body), isSatellite));
                rows.Add(_distanceRow);
                rows.Add(Row("Semi-major axis", isSatellite
                    ? $"{body.Elements.A * Ephemeris.AU_KM:N0} km"
                    : $"{body.Elements.A:F3} AU"));
                rows.Add(Row("Eccentricity", $"{body.Elements.E:F4}"));
                rows.Add(Row("Inclination", $"{body.Elements.I:F2}°"));
                rows.Add(Row("Orbital period", FormatPeriod(body.Elements.PeriodDays)));
            }

            if (Math.Abs(body.RotationHours) > 0.001 && body.Kind is "planet" or "dwarf" or "moon" or "star")
                rows.Add(Row("Rotation period", FormatPeriod(body.RotationHours * (1.0 / 24.0))));

            InspectorRows.ItemsSource = rows;
            InspectorDescription.Text = body.Description ?? "";
            InfoTabButton.IsEnabled = true;
            BreadcrumbText.Text = body.Name;
            BreadcrumbPanel.Visibility = Visibility.Visible;
            ShowPanel("info");
        }

        private static InspectorRow Row(string label, string value) => new InspectorRow { Label = label, Value = value };

        private static string FormatDistance(double au, bool preferKilometers = false)
        {
            if (preferKilometers || au < 0.001) return $"{au * Ephemeris.AU_KM:N0} km";
            return $"{au:F3} AU";
        }

        private static string FormatMass(double kg)
        {
            var exp = (int)Math.Floor(Math.Log10(kg));
            var mantissa = kg / Math.Pow(10, exp);
            return $"{mantissa:F2} × 10^{Superscript(exp)} kg";
        }

        private static string FormatPeriod(double days)
        {
            var d = Math.Abs(days);
            if (d >= 3652.569) return $"{d / 365.2569:F2} years";
            return $"{d:F2} days";
        }

        private static string Superscript(int n)
        {
            var sup = new[] { '⁰', '¹', '²', '³', '⁴', '⁵', '⁶', '⁷', '⁸', '⁹' };
            var sign = n < 0 ? "⁻" : "";
            var digits = Math.Abs(n).ToString(CultureInfo.InvariantCulture);
            return sign + new string(digits.Select(c => sup[c - '0']).ToArray());
        }

        private static string Capitalize(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];

        // ============================================================ Speed / time

        private void BuildSpeedPresets()
        {
            var presets = new (string, double)[]
            {
                ("REAL RATE", 1),
                ("1 MIN / SEC", 60),
                ("1 HOUR / SEC", 3600),
                ("1 DAY / SEC", 86400),
                ("7 DAYS / SEC", 604800),
                ("30 DAYS / SEC", 2592000),
                ("1 YEAR / SEC", 31557600),
                ("10 YEARS / SEC", 315576000),
                ("100 YEARS / SEC", 3155760000L),
                ("1000 YEARS / SEC", 31557600000L),
            };
            SpeedBox.ItemsSource = presets.Select(p => new SpeedPreset(p.Item1, p.Item2)).ToList();
            SpeedBox.DisplayMemberPath = nameof(SpeedPreset.Label);
            SpeedBox.SelectedIndex = 0;
        }

        private void SpeedBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SpeedBox.SelectedItem is SpeedPreset preset)
            {
                _speedSeconds = preset.Seconds;
                if (_webReady) TryPost(new { type = "setSpeed", speedSeconds = _speedSeconds });
            }
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            _playing = !_playing;
            PlayIcon.Glyph = _playing ? "\uE769" : "\uE768"; // Pause : Play
            if (_webReady) TryPost(new { type = "play", playing = _playing });
        }

        private void NowButton_Click(object sender, RoutedEventArgs e)
        {
            _simTimeUtc = DateTime.UtcNow;
            SyncTimeControls();
            if (_webReady) TryPost(new { type = "setTime", iso = _simTimeUtc.ToString("o") });
        }

        private void SyncTimeControls()
        {
            _syncingTime = true;
            DatePicker.Date = new DateTimeOffset(_simTimeUtc);
            TimePicker.SelectedTime = _simTimeUtc.TimeOfDay;
            _syncingTime = false;
        }

        private void DatePicker_DateChanged(object sender, DatePickerValueChangedEventArgs args)
        {
            if (_syncingTime) return;
            PostPickersTime();
        }

        private void TimePicker_TimeChanged(object sender, TimePickerSelectedValueChangedEventArgs args)
        {
            if (_syncingTime) return;
            PostPickersTime();
        }

        private void PostPickersTime()
        {
            var date = DatePicker.Date.Date; // DateTimeOffset
            var time = TimePicker.SelectedTime ?? TimeSpan.Zero;
            var utc = new DateTime(date.Year, date.Month, date.Day, time.Hours, time.Minutes, time.Seconds, DateTimeKind.Utc);
            _simTimeUtc = utc;
            if (_webReady) TryPost(new { type = "setTime", iso = utc.ToString("o") });
        }

        // ============================================================ List interactions

        private void BodiesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_syncingSelection) return;
            if (BodiesList.SelectedItem is BodyRow row)
                SelectBody(row.Id, focus: true);
        }

        private void FocusButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selected != null && _webReady)
                TryPost(new { type = "focus", id = _selected.Id });
        }

        private void ResetBodyOrientationButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null || !_webReady) return;
            TryPost(new { type = "resetBodyOrientation", id = _selected.Id });
            ShowToast($"{_selected.Name} returned to physical rotation");
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var query = SearchBox.Text?.Trim() ?? "";
            var matching = _rowsById.Values
                .Where(r => query.Length == 0 ||
                            r.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            r.Subtitle.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            BodyRows.Clear();
            foreach (var row in matching) BodyRows.Add(row);
        }

        private void Toggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_webReady) PostToggles();
        }

        // ============================================================ Immersive shell

        private void ShowPanel(string panel)
        {
            SidebarShell.Visibility = Visibility.Visible;
            RestoreSidebarButton.Visibility = Visibility.Collapsed;
            ExplorePanel.Visibility = panel == "explore" ? Visibility.Visible : Visibility.Collapsed;
            InfoPanel.Visibility = panel == "info" ? Visibility.Visible : Visibility.Collapsed;
            ViewPanel.Visibility = panel == "view" ? Visibility.Visible : Visibility.Collapsed;

            var active = (Brush)Application.Current.Resources["OverlayStrokeBrush"];
            var inactive = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            ExploreTabButton.Background = panel == "explore" ? active : inactive;
            InfoTabButton.Background = panel == "info" ? active : inactive;
            ViewTabButton.Background = panel == "view" ? active : inactive;
        }

        private void ExploreTabButton_Click(object sender, RoutedEventArgs e) => ShowPanel("explore");
        private void InfoTabButton_Click(object sender, RoutedEventArgs e) => ShowPanel("info");
        private void ViewTabButton_Click(object sender, RoutedEventArgs e) => ShowPanel("view");
        private void LayersButton_Click(object sender, RoutedEventArgs e) => ShowPanel("view");

        private void ToggleSidebarButton_Click(object sender, RoutedEventArgs e)
        {
            var open = SidebarShell.Visibility == Visibility.Visible;
            SidebarShell.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
            RestoreSidebarButton.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SearchTopButton_Click(object sender, RoutedEventArgs e)
        {
            ShowPanel("explore");
            SearchBox.Focus(FocusState.Programmatic);
        }

        private void ClearSelection()
        {
            _selected = null;
            _distanceRow = null;
            _syncingSelection = true;
            BodiesList.SelectedItem = null;
            _syncingSelection = false;
            InspectorBody.Visibility = Visibility.Collapsed;
            InspectorEmpty.Visibility = Visibility.Visible;
            InfoTabButton.IsEnabled = false;
            BreadcrumbPanel.Visibility = Visibility.Collapsed;
            ShowPanel("explore");
            if (_webReady) TryPost(new { type = "select", id = (string?)null });
        }

        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            ClearSelection();
            if (_webReady) TryPost(new { type = "resetView" });
        }

        private void ResetViewButton_Click(object sender, RoutedEventArgs e)
        {
            if (_webReady) TryPost(new { type = "resetView" });
            ShowToast("Solar system overview");
        }

        private void ZoomInButton_Click(object sender, RoutedEventArgs e)
        {
            if (_webReady) TryPost(new { type = "zoom", factor = 0.72 });
        }

        private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
        {
            if (_webReady) TryPost(new { type = "zoom", factor = 1.38 });
        }

        private void LightingButton_Click(object sender, RoutedEventArgs e)
        {
            _floodLighting = !_floodLighting;
            LightingButton.Background = _floodLighting
                ? (Brush)Application.Current.Resources["OverlayStrokeBrush"]
                : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            if (_webReady) TryPost(new { type = "setLighting", flood = _floodLighting });
            ShowToast(_floodLighting ? "Flood lighting on" : "Natural lighting on");
        }

        private void ShareButton_Click(object sender, RoutedEventArgs e)
        {
            var subject = _selected?.Name ?? "Solar System";
            var package = new DataPackage();
            package.SetText($"StarMap · {subject} · {_simTimeUtc:yyyy-MM-dd HH:mm:ss} UTC");
            Clipboard.SetContent(package);
            ShowToast("Scene summary copied");
        }

        private async void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = RootGrid.XamlRoot,
                Title = "Navigation",
                Content = "Drag empty space to orbit · Scroll to zoom · Click a body to inspect\nFocus a body, then drag it to turn the globe\n\nR  Return selected body to physical rotation\nSpace  Play / pause\nHome  Solar system overview\nCtrl+F  Search destinations\nEsc  Return to Explore",
                CloseButtonText = "Done",
                DefaultButton = ContentDialogButton.Close,
            };
            await dialog.ShowAsync();
        }

        private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            var focused = FocusManager.GetFocusedElement(RootGrid.XamlRoot);
            var editing = focused is Microsoft.UI.Xaml.Controls.TextBox
                or Microsoft.UI.Xaml.Controls.ComboBox
                or Microsoft.UI.Xaml.Controls.DatePicker
                or Microsoft.UI.Xaml.Controls.TimePicker;

            if (e.Key == VirtualKey.Space && !editing)
            {
                PlayButton_Click(PlayButton, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Home && !editing)
            {
                HomeButton_Click(HomeButton, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.R && !editing && _selected != null)
            {
                ResetBodyOrientationButton_Click(ResetBodyOrientationButton, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Escape)
            {
                ShowPanel("explore");
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.F &&
                     InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down))
            {
                SearchTopButton_Click(SearchBox, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private void ShowToast(string message)
        {
            ToastText.Text = message;
            ToastBar.Visibility = Visibility.Visible;
            _toastTimer.Stop();
            _toastTimer.Start();
        }
    }
}
