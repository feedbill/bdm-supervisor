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
    private bool _escrowOpen, _dropOpen;
    public bool NativeActive { get; private set; }
    private GateStatus Gates() => new(_escrowOpen, _dropOpen, false, false, false, false, false, false, 2);
    public GateStatus SetEscrowGate(bool open) { Require(); NativeActive = true; Delay(1300); _escrowOpen = open; return Gates(); }
    public GateStatus SetDropDoor(bool open) { Require(); NativeActive = true; Delay(2400); _dropOpen = open; return Gates(); }
    public GateStatus TestFlutter(int cycles) { Require(); NativeActive = true; Delay(2400 * cycles); return Gates(); }
    private int _tick;
    public SensorSnapshot ReadSensors()
    {
        Require(); NativeActive = true; _tick++;
        var sensors = NativeBne.SorterNames.ToDictionary(n => n, n => n is "ShutterSensorClose" or "DropDoorCloseSensor" or "LockCloseSensor" or "TurnRightSensor" or "TurnLeftSensor");
        var errors = NativeBne.MachineNames.ToDictionary(n => n, n => n == "TurnJamErr" && _tick % 4 == 0);
        var expand = NativeBne.ExpandNames.ToDictionary(n => n, n => false);
        return new SensorSnapshot(Gates(), sensors, errors, expand, 2, DateTime.Now, NativeBne.CabinetNames.ToDictionary(n => n, n => n == "InPosition" ? 1 : 0));
    }
    public void EndNative() { Delay(1500); NativeActive = false; _escrowOpen = _dropOpen = false; }
    public DepositCount CountDeposit(CancellationToken ct, Action<string> progress, int waitSeconds = 120)
    {
        Require();
        for (int i = 3; i > 0; i--) { ct.ThrowIfCancellationRequested(); progress($"Coloque los billetes en la boca de depósito… ({i} s)"); Delay(1000); }
        return new DepositCount([new CimNoteCount(569, "ARS", 10000, 8), new CimNoteCount(570, "ARS", 20000, 2)], 0);
    }
    public void ConfirmDeposit() { Require(); Delay(1500); _bagCount += 10; }
    public void ReturnDeposit() { Require(); Delay(1000); }
    public void Retract() { Require(); Delay(1500); _bagCount += 2; }
    public void BagExchange() { Require(); Delay(1000); _bagCount = 0; _log.Info("Mock: cambio de bolsa, contadores en 0"); }

    private void Require()
    {
        if (Mode != SupervisorMode.Active) throw new InvalidOperationException("Esta operación requiere el modo supervisor activo.");
    }
    private static void Delay(int ms) => Thread.Sleep(ms);
    public void Dispose() { }
}
