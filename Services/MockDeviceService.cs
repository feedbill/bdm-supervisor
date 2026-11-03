using BdmSupervisor.Xfs;

namespace BdmSupervisor.Services;

/// <summary>Equipo simulado para desarrollar y mostrar la UI sin hardware ni SP.</summary>
public sealed class MockDeviceService : IDeviceService
{
    private readonly LogService _log;
    private string _shutter = "CLOSED";
    private int _bagCount = 1240;
    private string _device = "ONLINE";

    public string Description => "SIMULADOR BDM-500 (sin hardware)";
    public bool VdmSupported => true;
    public SupervisorMode Mode { get; private set; } = SupervisorMode.Inactive;

    public MockDeviceService(LogService log) { _log = log; }

    public void Connect() => _log.Info("Mock: conectado");

    public CimStatus GetStatus() => new(
        _device, "CLOSED", "OK", "EMPTY", "NOITEMS", "OK", false,
        [new CimPosition("INFRONT", "NOTSUPP", "EMPTY", "OK", "EMPTY"),
         new CimPosition("OUTFRONT", _shutter, "EMPTY", "OK", "EMPTY")],
        "Firmware=BNE-S110M 2.3.1 | Template=ARS_2025_09", DateTime.Now);

    public IReadOnlyList<CimCashUnit> GetCashUnits() =>
    [
        new CimCashUnit(1, "CASHIN", "BAG01", _bagCount, _bagCount, 30000, _bagCount > 27000 ? "HIGH" : "OK"),
        new CimCashUnit(2, "REJECTCASSETTE", "REJ01", 3, 3, 100, "OK"),
    ];

    public string GetCapabilities() => "class=13 type=SELFSERVICEBILL maxCashInItems=500 shutter=True shutterControl=True safeDoor=True";

    public async Task EnterSupervisorAsync() { await Task.Delay(800); Mode = SupervisorMode.Active; _log.Info("Mock: VDM ENTERED"); }
    public void AssumeSupervisorActive() => Mode = SupervisorMode.Active;
    public async Task ExitSupervisorAsync() { await Task.Delay(500); Mode = SupervisorMode.Inactive; _log.Info("Mock: VDM EXITED"); }

    public void Reset() { Require(); Delay(2500); _device = "ONLINE"; _log.Info("Mock: RESET ok"); }
    public void OpenShutter() { Require(); Delay(300); _shutter = "OPEN"; }
    public void CloseShutter() { Require(); Delay(300); _shutter = "CLOSED"; }
    public void TestDeposit() { Require(); Delay(3000); _log.Info("Mock: test de depósito: 10 billetes contados, rollback ok"); }
    public void Retract() { Require(); Delay(1500); _bagCount += 2; }
    public void BagExchange() { Require(); Delay(1000); _bagCount = 0; _log.Info("Mock: cambio de bolsa, contadores en 0"); }

    private void Require()
    {
        if (Mode != SupervisorMode.Active) throw new InvalidOperationException("Esta operación requiere el modo supervisor activo.");
    }
    private static void Delay(int ms) => Thread.Sleep(ms);
    public void Dispose() { }
}
