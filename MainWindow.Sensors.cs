using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using BdmSupervisor.Services;

namespace BdmSupervisor;

/// <summary>
/// Pestaña "Sensores": diagrama del validador BNE-S110M (imagen del BneHostDemo de SNBC) con el estado de cada
/// sensor y la zona de cada atasco. Ubicación de los sensores tomada de la pantalla "Device Status" del demo
/// (SDK V2.7.28.9), en coordenadas de la imagen (352 x 383).
/// </summary>
public partial class MainWindow
{
    private sealed record SensorBox(string Name, double X, double Y, double Size, string[] Keys, bool Expand = false);
    private sealed record JamZone(string ErrorKey, double X1, double Y1, double X2, double Y2, bool Dashed = false);

    private static readonly SensorBox[] Boxes =
    [
        new("Sensor de alimentación", 200, 91, 40, ["FeedSensor"]),
        new("Billete en posición de alimentación", 140, 130, 40, ["FeedPutSensor"]),
        new("Sensores CIS derecho e izquierdo", 100, 180, 40, ["CISRightSensor", "CISLeftSensor"]),
        new("Contador de la boca de rechazo", 180, 180, 40, ["RejectCountSensor"]),
        new("Detección de la boca de rechazo", 250, 161, 40, ["RejectCheckSensor"]),
        new("Boca de rechazo llena", 300, 161, 40, ["RejectFullSensor"]),
        new("Sensor de espesor", 50, 220, 40, ["ThersoldSensor"]),
        new("Contador del escrow", 150, 230, 40, ["ExitCountSensor"]),
        new("Detección del escrow", 260, 260, 40, ["ExitCheckSensor"]),
        new("Curva derecho / izquierdo y HD trasero", 90, 270, 40, ["TurnRightSensor", "TurnLeftSensor", "BackHDSensor"]),
        new("Sensores antipesca y de caída", 260, 350, 40, NativeBne.ExpandNames, Expand: true),
        new("Compuerta de escrow abierta", 312, 267, 15, ["ShutterSensorOpen"]),
        new("Compuerta de escrow cerrada", 328, 267, 15, ["ShutterSensorClose"]),
        new("Traba de la compuerta abierta", 307, 307, 15, ["LockOpenSensor"]),
        new("Traba de la compuerta cerrada", 322, 307, 15, ["LockCloseSensor"]),
        new("Compuerta de caída abierta", 253, 317, 15, ["DropDoorOpenSensor"]),
        new("Compuerta de caída cerrada", 268, 317, 15, ["DropDoorCloseSensor"]),
    ];

    private static readonly JamZone[] Zones =
    [
        new("PaperJamErr", 6, 6, 346, 377, Dashed: true),
        new("CoverupErr", 20, 18, 345, 78),
        new("FeedJamErr", 112, 63, 228, 158),
        new("DataCollectJamErr", 22, 152, 128, 248),
        new("TurnJamErr", 60, 245, 135, 300),
        new("OutcashChanJamErr", 125, 205, 245, 260),
        new("OutcashJamErr", 232, 232, 292, 300),
        new("BackcashJamErr", 155, 133, 330, 205),
        new("ShutterErr", 300, 255, 340, 280),
        new("ShutterLockErr", 295, 295, 334, 320),
        new("DropDoorErr", 240, 305, 280, 330),
    ];

    private static readonly Dictionary<string, string> ErrorNames = new()
    {
        ["MainMotoErr"] = "Motor principal bloqueado",
        ["FeedMotoErr"] = "Motor de alimentación bloqueado",
        ["PaperJamErr"] = "Atasco de billete",
        ["FeedJamErr"] = "Atasco en la alimentación",
        ["DataCollectJamErr"] = "Atasco en captura de datos (CIS / magnético)",
        ["TurnJamErr"] = "Atasco en la curva del transporte",
        ["OutcashChanJamErr"] = "Atasco en el canal de salida al escrow",
        ["ElecSwitchErr"] = "Error del solenoide de desvío",
        ["OutcashJamErr"] = "Billetes trabados en el escrow",
        ["BackcashJamErr"] = "Billetes trabados en la boca de rechazo",
        ["CoverupErr"] = "Tapa superior levantada",
        ["CISErr"] = "Error del módulo CIS",
        ["MagneErr"] = "Error del módulo magnético",
        ["ThickErr"] = "Error del módulo de espesor",
        ["DropDoorErr"] = "Error de la compuerta de caída",
        ["ShutterErr"] = "Error de la compuerta de escrow",
        ["ShutterLockErr"] = "Error de la traba de la compuerta de escrow",
    };

    private static readonly string[] ProcessNames =
        ["—", "Encendido", "Listo", "Error", "Pendiente de inicializar", "Depósito en escrow completo", "Depósito en curso"];

    private static readonly Brush FreeBrush = Frozen(Color.FromArgb(200, 0x3C, 0x78, 0xD8));
    private static readonly Brush CoveredBrush = Frozen(Color.FromArgb(215, 0xD1, 0x33, 0x3B));
    private static readonly Brush NoDataBrush = Frozen(Color.FromArgb(110, 0x8A, 0x8A, 0x92));
    private static readonly Brush JamFill = Frozen(Color.FromArgb(60, 0xD1, 0x33, 0x3B));
    private static readonly Brush JamStroke = Frozen(Color.FromRgb(0xD1, 0x33, 0x3B));

    private readonly Dictionary<SensorBox, Rectangle> _sensorRects = [];
    private readonly Dictionary<string, Rectangle> _zoneRects = [];
    private readonly List<(string Cover, Rectangle Rect)> _coverRects = [];
    private static readonly Brush CoverFill = Frozen(Color.FromArgb(70, 0xF0, 0x8C, 0x00));
    private static readonly Brush CoverStroke = Frozen(Color.FromRgb(0xF0, 0x8C, 0x00));

    // Las tapas de acceso no tienen sensor propio: al abrirlas se separan las mitades de los sensores ópticos de esa
    // zona y quedan todos tapados a la vez. Medido en el BNE-S110M el 17/11/2026.
    private static readonly string[] SuperiorKeys = ["TurnRightSensor", "TurnLeftSensor", "RejectCountSensor", "RejectCheckSensor"];
    private static readonly string[] FrontalKeys = ["CISRightSensor", "CISLeftSensor", "ThersoldSensor"];
    private static readonly (string Cover, double X1, double Y1, double X2, double Y2)[] CoverAreas =
    [
        ("Superior", 55, 240, 140, 305), ("Superior", 155, 133, 330, 205),
        ("Frontal", 22, 105, 168, 250),
    ];
    private readonly DispatcherTimer _sensorTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _sensorBusy;

    private static Brush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    private void InitSensors()
    {
        foreach (var z in Zones)
        {
            var r = new Rectangle
            {
                Width = z.X2 - z.X1, Height = z.Y2 - z.Y1, RadiusX = 8, RadiusY = 8,
                Fill = z.Dashed ? Brushes.Transparent : JamFill, Stroke = JamStroke, StrokeThickness = z.Dashed ? 4 : 3,
                Visibility = Visibility.Collapsed, IsHitTestVisible = false,
                ToolTip = ErrorNames[z.ErrorKey],
            };
            if (z.Dashed) r.StrokeDashArray = [3, 2];
            r.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0.35, TimeSpan.FromSeconds(0.7)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
            Canvas.SetLeft(r, z.X1); Canvas.SetTop(r, z.Y1);
            SensorCanvas.Children.Add(r);
            _zoneRects[z.ErrorKey] = r;
        }
        foreach (var c in CoverAreas)
        {
            var r = new Rectangle
            {
                Width = c.X2 - c.X1, Height = c.Y2 - c.Y1, RadiusX = 8, RadiusY = 8, Fill = CoverFill, Stroke = CoverStroke,
                StrokeThickness = 3, StrokeDashArray = [4, 2], Visibility = Visibility.Collapsed, IsHitTestVisible = false,
            };
            Canvas.SetLeft(r, c.X1); Canvas.SetTop(r, c.Y1);
            SensorCanvas.Children.Add(r);
            _coverRects.Add((c.Cover, r));
        }
        foreach (var b in Boxes)
        {
            var r = new Rectangle { Width = b.Size, Height = b.Size, Fill = NoDataBrush, ToolTip = b.Name };
            Canvas.SetLeft(r, b.X - b.Size / 2); Canvas.SetTop(r, b.Y - b.Size / 2);
            SensorCanvas.Children.Add(r);
            _sensorRects[b] = r;
        }
        _sensorTimer.Tick += async (_, _) => await PollSensorsAsync();
        Tabs.SelectionChanged += (s, e) => { if (e.Source == Tabs && Tabs.SelectedItem == SensorTab) ShowXfsSensorInfo(); };
    }

    private void ClearSensors()
    {
        foreach (var r in _sensorRects.Values) r.Fill = NoDataBrush;
        foreach (var r in _zoneRects.Values) r.Visibility = Visibility.Collapsed;
        foreach (var (_, r) in _coverRects) r.Visibility = Visibility.Collapsed;
        SensorTimeText.Text = "";
        SensorCoverText.Text = "—";
        ShowXfsSensorInfo();
    }

    /// <summary>Sin mantenimiento directo: solo se muestra lo que informa XFS (último error del validador).</summary>
    private void ShowXfsSensorInfo()
    {
        if (_dev?.NativeActive == true) return;
        SensorCoveredText.Text = "—";
        if (_lastStatus is null) { SensorProcText.Text = "Sin lectura"; SensorErrText.Text = "—"; return; }
        SensorProcText.Text = $"Por XFS: {_lastStatus.Device} (lectura de sensores en vivo: pulse «Leer sensores»)";
        var (code, detail, jam) = ParseLastError(_lastStatus.Extra);
        bool hasErr = code.Trim('0', '-').Length > 0;
        SensorErrText.Foreground = hasErr ? Bad() : Ok();
        SensorErrText.Text = hasErr ? $"Último error informado por XFS: {(jam ? "ATASCO. " : "")}{code} {detail}".TrimEnd() : "Sin errores informados por XFS";
    }

    private void ShowSensors(SensorSnapshot s)
    {
        foreach (var (b, r) in _sensorRects)
        {
            var src = b.Expand ? s.Expand : s.Sensors;
            bool covered = b.Keys.Any(k => src.TryGetValue(k, out bool c) && c);
            r.Fill = covered ? CoveredBrush : FreeBrush;
        }
        foreach (var (key, r) in _zoneRects)
            r.Visibility = s.Errors.TryGetValue(key, out bool on) && on ? Visibility.Visible : Visibility.Collapsed;

        bool supOpen = SuperiorKeys.All(k => s.Sensors.GetValueOrDefault(k));
        bool froOpen = FrontalKeys.All(k => s.Sensors.GetValueOrDefault(k));
        foreach (var (c, r) in _coverRects)
            r.Visibility = (c == "Superior" ? supOpen : froOpen) ? Visibility.Visible : Visibility.Collapsed;

        var errs = new List<string>();
        if (supOpen) errs.Add("Tapa superior abierta: verificar que no queden billetes atascados y cerrar la tapa");
        if (froOpen) errs.Add("Tapa frontal abierta: verificar que no queden billetes atascados y cerrar la tapa");
        errs.AddRange(s.Errors.Where(e => e.Value).Select(e => ErrorNames.GetValueOrDefault(e.Key, e.Key)));
        SensorErrText.Foreground = errs.Count > 0 ? Bad() : Ok();
        SensorErrText.Text = errs.Count > 0 ? string.Join("\n", errs) : "Sin atascos ni errores";

        var covered2 = Boxes.Where(b => b.Keys.Any(k => (b.Expand ? s.Expand : s.Sensors).TryGetValue(k, out bool c) && c)).Select(b => b.Name).ToList();
        SensorCoveredText.Text = covered2.Count > 0 ? string.Join(" · ", covered2) : "Ninguno";

        // Tapas y puertas: la tapa superior viene como error de máquina (confirmado en el SDK).
        // El resto son sensores del gabinete del SDK V2.7; la polaridad (0/1) se confirma en el BDM-500 real.
        bool coverUp = s.Errors.TryGetValue("CoverupErr", out bool cu) && cu;
        int Cab(string k) => s.Cabinet.TryGetValue(k, out int v) ? v : -1;
        var cov = new List<string>
        {
            "Tapa superior: " + (supOpen || coverUp ? "ABIERTA — verificar atascos y cerrarla" : "cerrada"),
            "Tapa frontal: " + (froOpen ? "ABIERTA — verificar atascos y cerrarla" : "cerrada"),
            "Compuerta de escrow: " + (s.Gates.EscrowFault ? "ANOMALÍA" : s.Gates.EscrowOpen ? "abierta" : "cerrada") +
                " · traba " + (s.Sensors.GetValueOrDefault("LockCloseSensor") ? "cerrada" : s.Sensors.GetValueOrDefault("LockOpenSensor") ? "abierta" : "sin posición"),
            "Compuerta de caída: " + (s.Gates.DropDoorFault ? "ANOMALÍA" : s.Gates.DropDoorOpen ? "abierta" : "cerrada"),
            $"Bolsa en posición: {(Cab("InPosition") == 1 ? "sí" : "no")}",
            $"Puerta de la caja fuerte (sensor): {Cab("SafeShutter")} · traba de la puerta: {Cab("ShutterLock")}",
            $"Bolsa casi llena: {Cab("AlmostFull")} · bolsa llena: {Cab("Full")} · vibración: {Cab("Shake")}",
        };
        SensorCoverText.Text = string.Join("\n", cov);
        SensorCoverText.Foreground = coverUp || supOpen || froOpen ? Bad() : (Brush)FindResource("Dark");

        var g = s.Gates;
        string proc = g.Process >= 0 && g.Process < ProcessNames.Length ? ProcessNames[g.Process] : $"estado {g.Process}";
        var parts = new List<string> { proc };
        if (g.NotesInEntry) parts.Add("billetes en la boca de entrada");
        if (g.NotesInTransport) parts.Add("billetes en el transporte");
        if (g.NotesInEscrow) parts.Add("billetes en el escrow");
        if (g.NotesInReject) parts.Add("billetes en la boca de rechazo");
        parts.Add(g.EscrowFault ? "compuerta de escrow con anomalía" : g.EscrowOpen ? "escrow abierto" : "escrow cerrado");
        parts.Add(g.DropDoorFault ? "compuerta de caída con anomalía" : g.DropDoorOpen ? "compuerta de caída abierta" : "compuerta de caída cerrada");
        SensorProcText.Text = string.Join(" · ", parts);
        SensorTimeText.Text = $"Lectura: {s.Time:HH:mm:ss}";
        _escrowOpen = g.EscrowOpen; _dropOpen = g.DropDoorOpen;
        EscrowBtn.Content = _escrowOpen ? "Cerrar escrow" : "Abrir escrow";
        DropBtn.Content = _dropOpen ? "Cerrar compuerta de caída" : "Abrir compuerta de caída";
    }

    private async void ReadSensors_Click(object s, RoutedEventArgs e)
    {
        if (!ConfirmNative()) return;
        SensorSnapshot? snap = null;
        await Run("Leyendo sensores del validador...", () => snap = _dev!.ReadSensors());
        UpdateGateUi();
        if (snap != null) ShowSensors(snap);
    }

    private async void AutoSensors_Changed(object s, RoutedEventArgs e)
    {
        if (AutoSensors.IsChecked != true) { _sensorTimer.Stop(); return; }
        if (_dev?.NativeActive != true)
        {
            if (!ConfirmNative()) { AutoSensors.IsChecked = false; return; }
            SensorSnapshot? snap = null;
            await Run("Leyendo sensores del validador...", () => snap = _dev!.ReadSensors());
            UpdateGateUi();
            if (snap is null) { AutoSensors.IsChecked = false; return; }
            ShowSensors(snap);
        }
        _sensorTimer.Start();
    }

    private async Task PollSensorsAsync()
    {
        if (_sensorBusy || !IsEnabled || _dev?.NativeActive != true || Tabs.SelectedItem != SensorTab) return;
        _sensorBusy = true;
        try
        {
            var dev = _dev;
            var snap = await Task.Run(() => dev.ReadSensors());
            ShowSensors(snap);
        }
        catch (Exception ex)
        {
            AutoSensors.IsChecked = false;
            _log.Error("Lectura de sensores: " + ex.Message);
            StatusBar.Text = "Error leyendo sensores: " + ex.Message;
        }
        finally { _sensorBusy = false; }
    }
}
