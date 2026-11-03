using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using BdmSupervisor.Services;
using BdmSupervisor.Xfs;

namespace BdmSupervisor;

public partial class MainWindow : Window
{
    private readonly AppConfig _cfg;
    private readonly LogService _log;
    private IDeviceService? _dev;
    private readonly XfsEventWindow _events;
    private readonly SpParamsService _params;
    private CimStatus? _lastStatus;

    public const string WindowTitle = "BDM-500 Supervisor · Feedbill";
    public record ModuleRow(string Name, string Value, Brush Brush);

    public MainWindow()
    {
        InitializeComponent();
        _cfg = AppConfig.Load();
        _log = new LogService(_cfg.LogDir);
        _events = new XfsEventWindow();
        _params = new SpParamsService(_log);
        _log.LineWritten += line => Dispatcher.BeginInvoke(() =>
        {
            LogBox.AppendText(line + Environment.NewLine);
            LogBox.ScrollToEnd();
        });
        CfgText.Text = $"Mode={_cfg.Mode}\nCIM={_cfg.CimLogicalName}\nVDM={_cfg.VdmLogicalName}\nLogs={_cfg.LogDir}\nSP logs={_cfg.SpLogDir}\nSNBC tool={_cfg.SnbcToolPath}";
        Loaded += async (_, _) => { CheckTestTool(); await ConnectAsync(); };
        Closed += (_, _) => { _dev?.Dispose(); _events.Dispose(); };
    }

    // ---------- Conexión ----------
    private async Task ConnectAsync()
    {
        _dev = _cfg.Mode.Equals("Xfs", StringComparison.OrdinalIgnoreCase)
            ? new XfsDeviceService(_cfg, _log, _events)
            : new MockDeviceService(_log);

        await Run("Conectando al equipo...", () => _dev.Connect());
        DeviceText.Text = _dev.Description;
        VdmNote.Text = _dev.VdmSupported
            ? "VDM disponible: Softy liberará el equipo automáticamente al entrar."
            : "El Service Provider no expone VDM. Antes de entrar, detenga la aplicación bancaria (Softy) manualmente.";
        if (App.LaunchedBySp)
        {
            _dev.AssumeSupervisorActive();
            UpdateModeUi();
            Tabs.SelectedIndex = 1; // Supervisor / Diagnóstico
            StatusBar.Text = "Modo supervisor activado desde la aplicación bancaria";
        }
        await RefreshAsync();
    }

    // ---------- Estado ----------
    private async void Refresh_Click(object s, RoutedEventArgs e) => await RefreshAsync();

    private async Task RefreshAsync()
    {
        if (_dev is null) return;
        CimStatus? st = null;
        IReadOnlyList<CimCashUnit>? units = null;
        await Run("Leyendo estado...", () => { st = _dev.GetStatus(); units = _dev.GetCashUnits(); });
        if (st is null) return;
        _lastStatus = st;

        var rows = new List<ModuleRow>
        {
            new("Equipo", st.Device, st.Device == "ONLINE" ? Ok() : st.Device == "BUSY" ? Warn() : Bad()),
            new("Validador (BNE)", st.BanknoteReader, st.BanknoteReader == "OK" ? Ok() : Bad()),
            new("Aceptador / unidades", st.Acceptor, st.Acceptor == "OK" ? Ok() : Warn()),
            new("Escrow", st.IntermediateStacker, st.IntermediateStacker is "EMPTY" or "NOTSUPP" ? Ok() : Warn()),
            _cfg.SafeDoorMonitored
                ? new("Puerta de bóveda", st.SafeDoor, st.SafeDoor is "CLOSED" or "NOTSUPP" ? Ok() : Bad())
                : new("Puerta de bóveda", "No monitoreada", (Brush)FindResource("Muted")),
        };
        foreach (var p in st.Positions)
        {
            rows.Add(new($"Shutter {p.Position}", p.Shutter, p.Shutter is "CLOSED" or "NOTSUPP" ? Ok() : p.Shutter == "OPEN" ? Warn() : Bad()));
            rows.Add(new($"Transporte {p.Position}", p.Transport, p.Transport is "OK" or "NOTSUPP" ? Ok() : Bad()));
        }
        // No mostrar lo que el equipo no tiene (NOTSUPP)
        ModuleList.ItemsSource = rows.Where(r => r.Value != "NOTSUPP").ToList();
        CashUnitsGrid.ItemsSource = units;
        SpText.Text = _dev.Description;
        ExtraText.Text = string.IsNullOrWhiteSpace(st.Extra) ? "—" : st.Extra;
        TsText.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
        DeviceDot.Fill = rows[0].Brush;
    }

    // ---------- Supervisor ----------
    private async void Enter_Click(object s, RoutedEventArgs e)
    {
        if (_dev is null) return;
        await RunAsync("Solicitando modo supervisor (esperando que la app bancaria libere el equipo)...", () => _dev.EnterSupervisorAsync());
        UpdateModeUi();
    }

    private async void Exit_Click(object s, RoutedEventArgs e)
    {
        if (_dev is null) return;
        await RunAsync("Saliendo del modo supervisor...", () => _dev.ExitSupervisorAsync());
        UpdateModeUi();
        await RefreshAsync();
    }

    private void UpdateModeUi()
    {
        bool active = _dev?.Mode == SupervisorMode.Active;
        ModeText.Text = active ? "SUPERVISOR ACTIVO" : "SUPERVISOR INACTIVO";
        ModeBadge.Background = active ? (Brush)FindResource("Brand") : (Brush)FindResource("Muted");
        EnterBtn.IsEnabled = !active;
        ExitBtn.IsEnabled = active;
        DiagPanel.IsEnabled = active;
        DiagHint.Visibility = active ? Visibility.Collapsed : Visibility.Visible;
    }

    // ---------- Diagnóstico ----------
    private async void Reset_Click(object s, RoutedEventArgs e) { await Run("Reset en curso (puede tardar)...", () => _dev!.Reset()); await RefreshAsync(); }
    private async void OpenShutter_Click(object s, RoutedEventArgs e) { await Run("Abriendo shutter...", () => _dev!.OpenShutter()); await RefreshAsync(); }
    private async void CloseShutter_Click(object s, RoutedEventArgs e) { await Run("Cerrando shutter...", () => _dev!.CloseShutter()); await RefreshAsync(); }
    private async void TestDeposit_Click(object s, RoutedEventArgs e)
    {
        if (MessageBox.Show("Inserte billetes en la boca de depósito. Se contarán y se devolverán (no se depositan). ¿Continuar?",
                "Test de depósito", MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK) return;
        await Run("Test de depósito en curso...", () => _dev!.TestDeposit());
        await RefreshAsync();
    }
    private async void Retract_Click(object s, RoutedEventArgs e) { await Run("Retract...", () => _dev!.Retract()); await RefreshAsync(); }
    private async void BagExchange_Click(object s, RoutedEventArgs e)
    {
        if (MessageBox.Show("Esto pone en cero los contadores de la bolsa. Hágalo solo después de retirar la bolsa llena y colocar la nueva. ¿Continuar?",
                "Cambio de bolsa", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await Run("Registrando cambio de bolsa...", () => _dev!.BagExchange());
        await RefreshAsync();
    }
    private async void Caps_Click(object s, RoutedEventArgs e)
    {
        string caps = "";
        await Run("Leyendo capabilities...", () => caps = _dev!.GetCapabilities());
        _log.Info("CAPABILITIES: " + caps);
    }

    // ---------- Mantenimiento ----------
    private async void Export_Click(object s, RoutedEventArgs e)
    {
        string zip = "";
        string info = _lastStatus is null ? "(sin estado)" :
            $"SP: {_dev?.Description}\nDevice={_lastStatus.Device} BNR={_lastStatus.BanknoteReader} Acceptor={_lastStatus.Acceptor}\nExtra={_lastStatus.Extra}";
        await Run("Exportando bundle...", () => zip = _log.ExportBundle(_cfg.ExportDir, _cfg.SpLogDir, info));
        if (zip.Length > 0) Process.Start("explorer.exe", $"/select,\"{zip}\"");
    }

    private void OpenLogs_Click(object s, RoutedEventArgs e) => Process.Start("explorer.exe", _cfg.LogDir);

    private void SnbcTool_Click(object s, RoutedEventArgs e)
    {
        if (_dev?.Mode != SupervisorMode.Active)
        {
            MessageBox.Show("Entre en modo supervisor antes de abrir la herramienta del validador.", "Mantenimiento");
            return;
        }
        if (string.IsNullOrEmpty(_cfg.SnbcToolPath) || !File.Exists(_cfg.SnbcToolPath))
        {
            MessageBox.Show("No se encontró la herramienta SNBC. Configure Paths.SnbcToolPath en appsettings.json.", "Mantenimiento");
            return;
        }
        _log.Info($"Abriendo tool SNBC: {_cfg.SnbcToolPath}");
        Process.Start(new ProcessStartInfo(_cfg.SnbcToolPath) { UseShellExecute = true });
    }

    // ---------- TestTool ----------
    private void CheckTestTool()
    {
        if (!_params.SpInstalled) return;
        string? current = _params.ReadTestTool();
        if (current is null) return; // SP sin VDM
        string exe = Environment.ProcessPath ?? "";
        string expected = SpParamsService.ExpectedTestTool(exe);
        bool ok = current.Equals(expected, StringComparison.OrdinalIgnoreCase);
        TestToolBar.Visibility = ok ? Visibility.Collapsed : Visibility.Visible;
        if (!ok)
        {
            TestToolText.Text = $"El SP de SNBC lanza otra aplicación al entrar en modo supervisor (TestTool = \"{current}\"). " +
                                "Si SNBC actualizó el SP, el valor se perdió; con Reparar vuelve a apuntar a esta aplicación.";
            _log.Warn($"TestTool del SP no apunta a esta app: {current}");
        }
    }

    private void RepairTestTool_Click(object s, RoutedEventArgs e)
    {
        try
        {
            _params.RepairTestTool(Environment.ProcessPath ?? "");
            CheckTestTool();
            MessageBox.Show("TestTool actualizado. El SP lo toma en la próxima entrada al modo supervisor.", "BDM Supervisor");
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "BDM Supervisor", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    // ---------- Helpers ----------
    private Task Run(string status, Action work) => RunAsync(status, () => Task.Run(work));

    /// <summary>Ejecuta en el hilo de la UI (necesario para VDM, que recibe mensajes de ventana).</summary>
    private async Task RunAsync(string status, Func<Task> work)
    {
        StatusBar.Text = status;
        Busy.Visibility = Visibility.Visible;
        IsEnabled = false;
        try
        {
            await work();
            StatusBar.Text = "Listo";
        }
        catch (Exception ex)
        {
            _log.Error(ex.Message);
            StatusBar.Text = "Error: " + ex.Message;
            MessageBox.Show(ex.Message, "BDM Supervisor", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Busy.Visibility = Visibility.Collapsed;
            IsEnabled = true;
        }
    }

    private Brush Ok() => (Brush)FindResource("Ok");
    private Brush Warn() => (Brush)FindResource("Warn");
    private Brush Bad() => (Brush)FindResource("Bad");
}
