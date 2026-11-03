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
    private List<SpParam> _paramList = new();
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
        Loaded += async (_, _) => { CheckTestTool(); LoadParams(); await ConnectAsync(); };
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
            Tabs.SelectedIndex = 2; // Diagnóstico
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
        if (PinBox.Password != _cfg.SupervisorPin)
        {
            MessageBox.Show("PIN incorrecto.", "Supervisor", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
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
        if (!active) PinBox.Clear();
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

    // ---------- Parámetros ----------
    private void LoadParams()
    {
        try
        {
            _paramList = _params.SpInstalled ? _params.Load() : new List<SpParam>();
            ParamsGrid.ItemsSource = _paramList;
            if (!_params.SpInstalled) StatusBar.Text = "SP de SNBC no encontrado en " + SpParamsService.SpDir;
        }
        catch (Exception ex) { _log.Error("Parámetros: " + ex.Message); }
    }

    private void ParamsReload_Click(object s, RoutedEventArgs e) => LoadParams();

    private void ParamsGrid_CellEditEnding(object s, System.Windows.Controls.DataGridCellEditEndingEventArgs e)
    {
        if (e.Row.Item is SpParam p && !p.Editable) { e.Cancel = true; return; }
        if (_dev?.Mode != SupervisorMode.Active)
        {
            e.Cancel = true;
            MessageBox.Show("Para editar parámetros entre primero en modo supervisor.", "Parámetros");
        }
    }

    private void ParamsSave_Click(object s, RoutedEventArgs e)
    {
        if (_dev?.Mode != SupervisorMode.Active) { MessageBox.Show("Para guardar parámetros entre primero en modo supervisor.", "Parámetros"); return; }
        ParamsGrid.CommitEdit(); ParamsGrid.CommitEdit();
        var changes = _paramList.Where(p => p.Changed && p.Editable).ToList();
        if (changes.Count == 0) { StatusBar.Text = "Sin cambios"; return; }
        string resumen = string.Join("\n", changes.Select(c => $"• {c.Name}: {c.Original} → {c.Value}"));
        if (MessageBox.Show($"Se van a aplicar {changes.Count} cambio(s):\n\n{resumen}\n\nSe guarda copia de seguridad de cada archivo/valor. ¿Continuar?",
                "Guardar parámetros", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            var done = _params.Save(changes);
            ParamsGrid.Items.Refresh();
            MessageBox.Show("Aplicado:\n" + string.Join("\n", done) + "\n\nReinicie el SP para que tome los cambios (botón Reiniciar SP).", "Parámetros");
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Parámetros", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void ParamsGalicia_Click(object s, RoutedEventArgs e)
    {
        var perfil = new Dictionary<string, string>
        {
            ["SiuIsControl"] = "0", ["BoardCount"] = "1", ["ShutterDropDoorSurport"] = "0", ["SaveDoorSurport"] = "0",
        };
        foreach (var p in _paramList.Where(p => perfil.ContainsKey(p.Key))) p.Value = perfil[p.Key];
        ParamsGrid.Items.Refresh();
        StatusBar.Text = "Perfil Galicia cargado en la grilla; revise y pulse Guardar cambios";
    }

    private async void RestartSp_Click(object s, RoutedEventArgs e)
    {
        if (MessageBox.Show("Se cierran los procesos del SP de SNBC (SNBC_SPCRM, SNBC_SPVDM) y se reconecta. La aplicación bancaria debe estar detenida. ¿Continuar?",
                "Reiniciar SP", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await Run("Reiniciando SP...", () =>
        {
            _dev?.Dispose(); _dev = null;
            foreach (var name in new[] { "SNBC_SPCRM", "SNBC_SPVDM", "STM_IO_SRV" })
                foreach (var pr in Process.GetProcessesByName(name)) { try { pr.Kill(); pr.WaitForExit(5000); } catch { } }
            Thread.Sleep(2000);
        });
        await ConnectAsync();
        UpdateModeUi();
    }

    private async void ParamsExport_Click(object s, RoutedEventArgs e)
    {
        string path = "";
        await Run("Exportando...", () =>
        {
            Directory.CreateDirectory(_cfg.ExportDir);
            path = Path.Combine(_cfg.ExportDir, $"parametros_SP_{Environment.MachineName}_{DateTime.Now:yyyyMMdd_HHmm}.txt");
            File.WriteAllLines(path, _paramList.Select(p => $"{p.Group,-16} {p.Name,-40} = {p.Value,-30} ({p.Location} {p.Key})"));
        });
        if (path.Length > 0) Process.Start("explorer.exe", $"/select,\"{path}\"");
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
