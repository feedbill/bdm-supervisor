using System.Diagnostics;
using BdmSupervisor.Xfs;

namespace BdmSupervisor.Services;

/// <summary>Implementación real contra el Service Provider XFS de SNBC.</summary>
public sealed class XfsDeviceService : IDeviceService
{
    private readonly AppConfig _cfg;
    private readonly LogService _log;
    private CimService? _cim;
    private VdmService? _vdm;

    public string Description { get; private set; } = "No conectado";
    public bool VdmSupported { get; private set; }
    public SupervisorMode Mode { get; private set; } = SupervisorMode.Inactive;

    private readonly IXfsEventSource _events;

    public XfsDeviceService(AppConfig cfg, LogService log, IXfsEventSource events) { _cfg = cfg; _log = log; _events = events; }

    public void Connect()
    {
        _cim = OpenCim();
        Description = $"{_cim.SpDescription} v{_cim.SpVersion} ({_cfg.CimLogicalName})";

        try
        {
            var vdmSession = new XfsSession(_cfg.VdmLogicalName, _cfg.AppId, _cfg.TimeoutMs, _cfg.TraceLevel, _log);
            _vdm = new VdmService(vdmSession, _log);
            _vdm.Attach(_events);
            VdmSupported = true;
            _log.Info($"VDM disponible. Estado: {_vdm.GetStatus()}");
        }
        catch (XfsException ex)
        {
            VdmSupported = false;
            _log.Warn($"Servicio VDM no disponible ({ex.Message}). Se usará modo exclusivo: la app bancaria debe estar detenida.");
        }
    }

    /// <summary>Abre el servicio CIM. Si el SP está arrancando (tarda ~30 s) reintenta en vez de fallar.</summary>
    private CimService OpenCim()
    {
        XfsException? last = null;
        for (int i = 0; i < 5; i++)
        {
            try { return new CimService(new XfsSession(_cfg.CimLogicalName, _cfg.AppId, _cfg.TimeoutMs, _cfg.TraceLevel, _log), _log); }
            catch (XfsException ex) when (ex.HResult2 is XfsConst.WFS_ERR_TIMEOUT or XfsConst.WFS_ERR_DEV_NOT_READY or XfsConst.WFS_ERR_CONNECTION_LOST)
            {
                last = ex;
                _log.Warn($"El Service Provider todavía está iniciando ({ex.Message}). Reintento {i + 1} de 5...");
                Thread.Sleep(4000);
            }
        }
        throw last!;
    }

    // ---- Mantenimiento directo con el SDK del validador ----
    private NativeBne? _native;
    public bool NativeActive => _native != null;

    private NativeBne Native()
    {
        Require();
        if (_native != null) return _native;
        _log.Warn("Mantenimiento directo: se detiene el Service Provider del validador para mover las compuertas con el SDK de SNBC.");
        _cim?.Dispose(); _cim = null;
        foreach (var p in Process.GetProcessesByName("SNBC_SPCRM")) { try { p.Kill(); p.WaitForExit(5000); } catch { } }
        Thread.Sleep(1500);
        _native = NativeBne.Open(_log);
        return _native;
    }

    public GateStatus SetEscrowGate(bool open) { var n = Native(); n.EscrowGate(open); return n.GetGates(); }
    public GateStatus SetDropDoor(bool open) { var n = Native(); n.DropDoor(open); return n.GetGates(); }

    public GateStatus TestFlutter(int cycles)
    {
        var n = Native();
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < cycles; i++) { n.DropDoor(true); n.DropDoor(false); }
        _log.Info($"Aleteo: {cycles} ciclo(s) de la compuerta de caída en {sw.Elapsed.TotalSeconds:F1} s");
        return n.GetGates();
    }

    public void EndNative()
    {
        if (_native == null) return;
        try { var g = _native.GetGates(); if (g.DropDoorOpen) _native.DropDoor(false); if (g.EscrowOpen) _native.EscrowGate(false); } catch { }
        _native.Dispose(); _native = null;
        Thread.Sleep(1000);
        _log.Info("Volviendo a operación normal: el Service Provider retoma el validador (tarda hasta un minuto)...");
        _cim = OpenCim();
    }

    public CimStatus GetStatus()
    {
        if (_native == null) return Cim.GetStatus();
        var g = _native.GetGates();
        string St(bool open, bool fault) => fault ? "JAMMED" : open ? "OPEN" : "CLOSED";
        return new CimStatus("MANTENIMIENTO DIRECTO", "NOTSUPP", "OK",
            g.NotesInEscrow ? "NOTEMPTY" : "EMPTY", g.NotesInEntry ? "CUSTOMERACCESS" : "NOITEMS", g.NotesInTransport ? "INOP" : "OK", false,
            [new CimPosition("ESCROW", St(g.EscrowOpen, g.EscrowFault), g.NotesInEscrow ? "NOTEMPTY" : "EMPTY", "NOTSUPP", "NOTSUPP"),
             new CimPosition("CAIDA", St(g.DropDoorOpen, g.DropDoorFault), g.NotesInReject ? "NOTEMPTY" : "EMPTY", "NOTSUPP", "NOTSUPP")],
            g.NotesInTransport ? "LastErrorCode=TRANSPORTE | LastErrorDetail=Billetes en el transporte (posible atasco/jam)" : "", DateTime.Now);
    }

    public IReadOnlyList<CimCashUnit> GetCashUnits() => _native == null ? Cim.GetCashUnits() : [];
    public string GetCapabilities() => Cim.GetCapabilitiesRaw();

    public async Task EnterSupervisorAsync()
    {
        Mode = SupervisorMode.Entering;
        try
        {
            if (VdmSupported) await _vdm!.EnterModeAsync();
            else _log.Warn("Entrando en modo supervisor sin VDM: verifique que Softy esté detenida.");
            Mode = SupervisorMode.Active;
            if (VdmSupported) _log.Info($"VDM estado: {_vdm!.GetStatus()}");
        }
        catch { Mode = SupervisorMode.Inactive; throw; }
    }

    public void AssumeSupervisorActive()
    {
        Mode = SupervisorMode.Active;
        if (VdmSupported) _log.Info($"Lanzados por el SP; VDM estado: {_vdm!.GetStatus()}");
    }

    public async Task ExitSupervisorAsync()
    {
        Mode = SupervisorMode.Exiting;
        try
        {
            if (_native != null) await Task.Run(EndNative);
            if (VdmSupported) await _vdm!.ExitModeAsync();
            Mode = SupervisorMode.Inactive;
            if (VdmSupported) _log.Info($"VDM estado: {_vdm!.GetStatus()}");
        }
        catch { Mode = SupervisorMode.Active; throw; }
    }

    public void Reset() { Require(); Cim.Reset(); }

    public DepositCount CountDeposit(CancellationToken ct, Action<string> progress, int waitSeconds = 120)
    {
        Require();
        Cim.CashInStart();
        var deadline = DateTime.Now.AddSeconds(waitSeconds);
        try
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    // Con la boca vacía el SP devuelve TIMEOUT/NOITEMS a los ~2 s: no es un error, se sigue esperando
                    Cim.CashIn(15000, quiet: true);
                    var st = Cim.GetCashInStatus();
                    if (st.Notes.Sum(n => n.Count) > 0 || st.Refused > 0)
                    {
                        _log.Info($"Prueba de depósito: {st.Notes.Sum(n => n.Count)} billetes contados, {st.Refused} rechazados");
                        return new DepositCount(st.Notes, st.Refused);
                    }
                }
                catch (XfsException ex) when (ex.HResult2 is XfsConst.WFS_ERR_TIMEOUT or XfsConst.WFS_ERR_CIM_NOITEMS) { }
                int left = (int)(deadline - DateTime.Now).TotalSeconds;
                if (left <= 0) throw new TimeoutException("No se colocaron billetes en la boca de depósito. La prueba se canceló.");
                progress($"Coloque los billetes en la boca de depósito… ({left} s)");
                Thread.Sleep(600);
            }
        }
        catch
        {
            // No dejar el depósito abierto en el SP si se cancela o falla
            try { Cim.CashInRollback(); } catch { try { Cim.CashInEnd(); } catch { } }
            throw;
        }
    }

    public void ConfirmDeposit() { Require(); Cim.CashInEnd(); }
    public void ReturnDeposit() { Require(); Cim.CashInRollback(); }

    public void Retract() { Require(); Cim.Retract(); }

    public void BagExchange()
    {
        Require();
        Cim.StartExchange();
        Cim.EndExchange();
    }

    private CimService Cim => _cim ?? throw new InvalidOperationException(_native != null
        ? "El equipo está en mantenimiento directo (compuertas). Pulse «Volver a operación normal» para usar esta función."
        : "No conectado al SP XFS");

    private void Require()
    {
        if (Mode != SupervisorMode.Active)
            throw new InvalidOperationException("Esta operación requiere el modo supervisor activo.");
    }

    public void Dispose()
    {
        try { if (Mode == SupervisorMode.Active) ExitSupervisorAsync().Wait(5000); } catch { }
        try { _native?.Dispose(); } catch { }
        _native = null;
        _vdm?.Dispose();
        _cim?.Dispose();
    }
}
