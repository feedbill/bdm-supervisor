using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Text.RegularExpressions;
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
    private bool _escrowOpen, _dropOpen;   // último estado conocido de las compuertas (mantenimiento directo)

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
        CfgText.Text = $"Mode={_cfg.Mode}\nCIM={_cfg.CimLogicalName}\nVDM={_cfg.VdmLogicalName}\nLogs={_cfg.LogDir}\nSP logs={EffectiveSpLogDir()}\nSNBC tool={EffectiveSnbcTool()}";
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
            new("Equipo", st.Device, st.Device == "ONLINE" ? Ok() : st.Device is "BUSY" or "MANTENIMIENTO DIRECTO" ? Warn() : Bad()),
            new("Validador (BNE)", st.BanknoteReader, st.BanknoteReader == "OK" ? Ok() : Bad()),
            new("Aceptador / unidades", st.Acceptor, st.Acceptor == "OK" ? Ok() : Warn()),
            new("Escrow", st.IntermediateStacker, st.IntermediateStacker is "EMPTY" or "NOTSUPP" ? Ok() : Warn()),
            _cfg.SafeDoorMonitored
                ? new("Puerta de bóveda", st.SafeDoor, st.SafeDoor is "CLOSED" or "NOTSUPP" ? Ok() : Bad())
                : new("Puerta de bóveda", "No monitoreada", (Brush)FindResource("Muted")),
        };
        foreach (var p in st.Positions)
        {
            string nombre = p.Position switch { "ESCROW" => "Compuerta de escrow", "CAIDA" => "Compuerta de caída", _ => $"Shutter {p.Position}" };
            if (p.Position == "ESCROW") _escrowOpen = p.Shutter == "OPEN";
            if (p.Position == "CAIDA") _dropOpen = p.Shutter == "OPEN";
            rows.Add(new(nombre, p.Shutter, p.Shutter is "CLOSED" or "NOTSUPP" ? Ok() : p.Shutter == "OPEN" ? Warn() : Bad()));
            rows.Add(new($"Transporte {p.Position}", p.Transport, p.Transport is "OK" or "NOTSUPP" ? Ok() : Bad()));
        }
        // Último error / atasco informado por el SP (LastErrorCode / LastErrorDetail del campo Extra)
        var (errCode, errDetail, jam) = ParseLastError(st.Extra);
        bool hasErr = errCode.Length > 0 && errCode.Trim('0', '-').Length > 0;
        if (jam || st.Positions.Any(p => p.Shutter == "JAMMED"))
            rows.Add(new("Atasco", "DETECTADO", Bad()));
        rows.Add(new("Último error del validador", hasErr ? $"{errCode} {errDetail}".Trim() : "Sin errores", hasErr ? Bad() : Ok()));

        UpdateGateUi();

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
        GatePanel.IsEnabled = active;
        UpdateGateUi();
        DiagHint.Visibility = active ? Visibility.Collapsed : Visibility.Visible;
    }

    // ---------- Diagnóstico ----------
    private async void Reset_Click(object s, RoutedEventArgs e) { await Run("Reset en curso (puede tardar)...", () => _dev!.Reset()); await RefreshAsync(); }
    // ---------- Compuertas y aleteo (mantenimiento directo con el SDK) ----------
    private void UpdateGateUi()
    {
        bool native = _dev?.NativeActive == true;
        if (!native) { _escrowOpen = false; _dropOpen = false; }
        EscrowBtn.Content = _escrowOpen ? "Cerrar escrow" : "Abrir escrow";
        DropBtn.Content = _dropOpen ? "Cerrar compuerta de caída" : "Abrir compuerta de caída";
        NormalBtn.Visibility = native ? Visibility.Visible : Visibility.Collapsed;
    }

    private bool ConfirmNative()
    {
        if (_dev?.NativeActive == true) return true;
        return MessageBox.Show(this, "Para mover las compuertas se detiene el Service Provider del validador. " +
            "Mientras tanto no se puede depositar. Al terminar pulse «Volver a operación normal».\n\n¿Continuar?",
            "Mantenimiento directo", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    private async Task GateOp(string status, Func<GateStatus> op)
    {
        if (!ConfirmNative()) return;
        GateStatus? g = null;
        await Run(status, () => g = op());
        if (g != null)
        {
            _escrowOpen = g.EscrowOpen; _dropOpen = g.DropDoorOpen;
            if (g.EscrowFault || g.DropDoorFault) _log.Error("El validador informa una anomalía en " + (g.EscrowFault ? "la compuerta de escrow" : "la compuerta de caída"));
        }
        await RefreshAsync();
    }

    private async void ToggleEscrow_Click(object s, RoutedEventArgs e)
    {
        bool open = !_escrowOpen;
        await GateOp(open ? "Abriendo la compuerta de escrow..." : "Cerrando la compuerta de escrow...", () => _dev!.SetEscrowGate(open));
    }

    private async void ToggleDrop_Click(object s, RoutedEventArgs e)
    {
        bool open = !_dropOpen;
        await GateOp(open ? "Abriendo la compuerta de caída..." : "Cerrando la compuerta de caída...", () => _dev!.SetDropDoor(open));
    }

    private async void Flutter_Click(object s, RoutedEventArgs e)
    {
        // Mismos ciclos que hace el equipo al confirmar un depósito: 1 + los aleteos tras prensado correcto
        string? cfg = SpParamsService.ReadIni(Path.Combine(SpParamsService.SpDir, @"SnbcTCR\BNEHostInifile\Config.ini"), "StoreCash", "successretry");
        int extra = int.TryParse(cfg, out int v) && v is >= 0 and <= 10 ? v : 1;
        int ciclos = 1 + extra;
        await GateOp($"Aleteo: {ciclos} ciclo(s) de la compuerta de caída...", () => _dev!.TestFlutter(ciclos));
        StatusBar.Text = $"Aleteo terminado: {ciclos} ciclo(s) de apertura y cierre de la compuerta de caída";
    }

    private async void EndNative_Click(object s, RoutedEventArgs e)
    {
        await Run("Volviendo a operación normal (el Service Provider tarda hasta un minuto)...", () => _dev!.EndNative());
        await RefreshAsync();
    }

    private async void TestDeposit_Click(object s, RoutedEventArgs e)
    {
        using var cts = new CancellationTokenSource();
        var msg = new TextBlock { Text = "Coloque los billetes en la boca de depósito…", FontSize = 16, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) };
        var cancel = new Button { Content = "Cancelar", MinWidth = 140, HorizontalAlignment = HorizontalAlignment.Right };
        cancel.Click += (_, _) => { cts.Cancel(); cancel.IsEnabled = false; msg.Text = "Cancelando…"; };
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(msg); panel.Children.Add(cancel);
        var dlg = new Window
        {
            Title = "Prueba de depósito", Owner = this, Content = panel, Width = 460, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Topmost = true
        };
        dlg.Closing += (_, _) => { if (!cts.IsCancellationRequested) cts.Cancel(); };

        DepositCount? count = null;
        dlg.Show();
        try
        {
            await Run("Prueba de depósito: esperando billetes…",
                () => count = _dev!.CountDeposit(cts.Token, m => Dispatcher.BeginInvoke(() => { msg.Text = m; StatusBar.Text = m; })));
        }
        finally { try { dlg.Close(); } catch { } }

        if (count is null) { await RefreshAsync(); return; }

        int total = count.Notes.Sum(n => n.Count);
        long monto = count.Notes.Sum(n => n.Value * n.Count);
        string moneda = count.Notes.Select(n => n.Currency).FirstOrDefault() ?? "";
        string detalle = string.Join("\n", count.Notes.OrderBy(n => n.Value).Select(n => $"   {n.Count} × {n.Currency} {n.Value:N0}"));
        string texto = $"Billetes contados: {total}\n{detalle}\nTotal: {moneda} {monto:N0}" +
                       (count.Refused > 0 ? $"\nRechazados: {count.Refused} (quedan en la salida de rechazo)" : "") +
                       "\n\nSí = depositar (pasan a la bolsa)\nNo = devolver los billetes";
        _log.Info("Prueba de depósito: " + texto.Replace("\n", " | "));
        var r = MessageBox.Show(this, texto, "Prueba de depósito", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r == MessageBoxResult.Yes) await Run("Depositando (los billetes pasan a la bolsa)…", () => _dev!.ConfirmDeposit());
        else await Run("Devolviendo los billetes…", () => _dev!.ReturnDeposit());
        await RefreshAsync();
    }

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
        if (caps.Length > 0)
            MessageBox.Show(this, caps.Replace(" ", "\n"), "Capabilities del validador", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ---------- Mantenimiento ----------
    private async void Export_Click(object s, RoutedEventArgs e)
    {
        string zip = "";
        string info = _lastStatus is null ? "(sin estado)" :
            $"SP: {_dev?.Description}\nDevice={_lastStatus.Device} BNR={_lastStatus.BanknoteReader} Acceptor={_lastStatus.Acceptor}\nExtra={_lastStatus.Extra}";
        string? spLogs = EffectiveSpLogDir();
        await Run("Exportando bundle...", () => zip = _log.ExportBundle(_cfg.ExportDir, spLogs, info));
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
        string? tool = EffectiveSnbcTool();
        if (tool is null)
        {
            MessageBox.Show("No se encontró la herramienta SNBC. Configure Paths.SnbcToolPath en appsettings.json.", "Mantenimiento");
            return;
        }
        _log.Info($"Abriendo tool SNBC: {tool}");
        Process.Start(new ProcessStartInfo(tool) { UseShellExecute = true });
    }

    /// <summary>Carpeta real de logs del SP: la que el propio SP tiene configurada; si no, la de appsettings.</summary>
    private string? EffectiveSpLogDir()
    {
        string? reg = SpParamsService.ReadReg(@"HKLM32:SOFTWARE\SNBC\SpLog", "LogPath");
        if (!string.IsNullOrEmpty(reg))
        {
            string dir = reg.TrimEnd('\\');
            if (Path.GetFileName(dir).Equals("sp", StringComparison.OrdinalIgnoreCase)) dir = Path.GetDirectoryName(dir) ?? dir;
            if (Directory.Exists(dir)) return dir;
        }
        return _cfg.SpLogDir;
    }

    /// <summary>Tester de SNBC: el configurado; si no existe, el original que instala el módulo VDM.</summary>
    private string? EffectiveSnbcTool()
    {
        if (!string.IsNullOrEmpty(_cfg.SnbcToolPath) && File.Exists(_cfg.SnbcToolPath)) return _cfg.SnbcToolPath;
        string alt = Path.Combine(SpParamsService.SpDir, "VDM", "DeviceTestorBySP.exe");
        return File.Exists(alt) ? alt : null;
    }

    /// <summary>Extrae LastErrorCode / LastErrorDetail del Extra del SP y detecta si describe un atasco.</summary>
    private static (string Code, string Detail, bool Jam) ParseLastError(string extra)
    {
        string code = Regex.Match(extra ?? "", @"LastErrorCode=([^|]*)").Groups[1].Value.Trim();
        string detail = Regex.Match(extra ?? "", @"LastErrorDetail=([^|]*)").Groups[1].Value.Trim();
        if (detail.Equals("Success", StringComparison.OrdinalIgnoreCase)) detail = "";
        bool jam = Regex.IsMatch(detail, @"jam|atasc|stuck|block|caught|remain", RegexOptions.IgnoreCase);
        return (code, detail, jam);
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
        catch (OperationCanceledException)
        {
            _log.Info("Operación cancelada por el usuario");
            StatusBar.Text = "Cancelado";
        }
        catch (Exception ex)
        {
            _log.Error(ex.Message);
            // Si el equipo informa un error propio (atasco, etc.), se agrega al mensaje
            string extraMsg = "";
            try
            {
                var dev = _dev;
                if (dev != null && ex is not TimeoutException)
                {
                    var st = await Task.Run(() => dev.GetStatus());
                    var (code, detail, jam) = ParseLastError(st.Extra);
                    if (code.Trim('0', '-').Length > 0)
                    {
                        extraMsg = $"\n\nEl validador informa: {(jam ? "ATASCO. " : "")}{code} {detail}".TrimEnd();
                        _log.Error($"Validador: {(jam ? "ATASCO " : "")}{code} {detail}");
                    }
                }
            }
            catch { }
            StatusBar.Text = "Error: " + ex.Message;
            MessageBox.Show(ex.Message + extraMsg, "BDM Supervisor", MessageBoxButton.OK, MessageBoxImage.Error);
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
