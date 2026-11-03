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
        var cimSession = new XfsSession(_cfg.CimLogicalName, _cfg.AppId, _cfg.TimeoutMs, _cfg.TraceLevel, _log);
        _cim = new CimService(cimSession, _log);
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

    public CimStatus GetStatus() => Cim.GetStatus();
    public IReadOnlyList<CimCashUnit> GetCashUnits() => Cim.GetCashUnits();
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
            if (VdmSupported) await _vdm!.ExitModeAsync();
            Mode = SupervisorMode.Inactive;
            if (VdmSupported) _log.Info($"VDM estado: {_vdm!.GetStatus()}");
        }
        catch { Mode = SupervisorMode.Active; throw; }
    }

    public void Reset() { Require(); Cim.Reset(); }
    public void OpenShutter() { Require(); Cim.OpenShutter(); }
    public void CloseShutter() { Require(); Cim.CloseShutter(); }

    public void TestDeposit()
    {
        Require();
        Cim.CashInStart();
        try
        {
            Cim.CashIn();
        }
        finally
        {
            // Siempre devolvemos los billetes: es un test, no un depósito
            Cim.CashInRollback();
        }
    }

    public void Retract() { Require(); Cim.Retract(); }

    public void BagExchange()
    {
        Require();
        Cim.StartExchange();
        Cim.EndExchange();
    }

    private CimService Cim => _cim ?? throw new InvalidOperationException("No conectado al SP XFS");

    private void Require()
    {
        if (Mode != SupervisorMode.Active)
            throw new InvalidOperationException("Esta operación requiere el modo supervisor activo.");
    }

    public void Dispose()
    {
        try { if (Mode == SupervisorMode.Active) ExitSupervisorAsync().Wait(5000); } catch { }
        _vdm?.Dispose();
        _cim?.Dispose();
    }
}
