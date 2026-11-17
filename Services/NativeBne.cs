using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace BdmSupervisor.Services;

/// <summary>Estado de compuertas y sensores leído directo del validador (SDK BNEHostDll, GetDevStatus).</summary>
public sealed record GateStatus(bool EscrowOpen, bool DropDoorOpen, bool EscrowFault, bool DropDoorFault,
    bool NotesInEntry, bool NotesInEscrow, bool NotesInReject, bool NotesInTransport, int Process);

/// <summary>
/// Lectura de mantenimiento del validador (GetDevStatusService): sensores del recorrido, sensores antipesca
/// y errores de máquina (atascos por zona, motores, tapa, compuertas).
/// Sensores: true = tapado (hay algo delante). Errores: true = activo.
/// </summary>
public sealed record SensorSnapshot(GateStatus Gates, IReadOnlyDictionary<string, bool> Sensors,
    IReadOnlyDictionary<string, bool> Errors, IReadOnlyDictionary<string, bool> Expand, int SystemStatus, DateTime Time,
    IReadOnlyDictionary<string, int> Cabinet);

/// <summary>
/// Acceso directo al validador BNE-S110M con el SDK de SNBC (BNEHostDll.dll, el mismo que usa el SP).
/// Solo para mantenimiento: el SDK y el Service Provider no pueden tener el equipo abierto a la vez,
/// así que antes de abrir hay que detener SNBC_SPCRM.exe y al terminar se cierra para que el SP lo retome.
/// Probado el 17/11/2026 contra un BNE-S110M: ShutterControl = compuerta de escrow, DropDoorControl = compuerta de caída
/// (cControl 0 = abrir, 1 = cerrar).
/// </summary>
public sealed class NativeBne : IDisposable
{
    public static readonly string Dir = Path.Combine(SpParamsService.SpDir, "SnbcTCR");
    private const string Dll = "BNEHostDll.dll";

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct Err
    {
        public uint Code; public uint Type;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 96)] public byte[] Msg;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)] public byte[] Res;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct Ctl
    {
        public byte Control; public byte Retry;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] public byte[] Res;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct DevStatus
    {
        public byte EntryHasNotes, EscrowHasNotes, ExitHasNotes, Shutter, DropDoor, ProcessStatus, ChannelHas, Escrow;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] Res;
    }

    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool SetDllDirectory(string? path);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] private static extern IntPtr OpenDevice(string name);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)] private static extern int CloseDevice(string name);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int GetDevStatus(IntPtr h, ref DevStatus st, ref Err e);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int GetDevStatusService(IntPtr h, [In, Out] byte[] st, ref Err e);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int ShutterControl(IntPtr h, Ctl c, ref Err e);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int DropDoorControl(IntPtr h, Ctl c, ref Err e);

    private IntPtr _h;
    private readonly object _sync = new();
    private readonly LogService _log;
    private readonly string _prevDir;

    private NativeBne(LogService log, string prevDir) { _log = log; _prevDir = prevDir; }

    public static NativeBne Open(LogService log)
    {
        string full = Path.Combine(Dir, Dll);
        if (!File.Exists(full)) throw new InvalidOperationException($"No se encontró el SDK del validador ({full}).");
        string prev = Environment.CurrentDirectory;
        // El SDK busca su configuración (BNEHostInifile) y sus dependencias en su propia carpeta
        Environment.CurrentDirectory = Dir;
        SetDllDirectory(Dir);
        NativeLibrary.Load(full);
        var n = new NativeBne(log, prev);
        n._h = OpenDevice("");
        if (n._h == IntPtr.Zero)
        {
            Environment.CurrentDirectory = prev;
            throw new InvalidOperationException("El SDK no pudo abrir el validador. Verifique que el Service Provider esté detenido y el USB conectado.");
        }
        log.Info("Mantenimiento directo: validador abierto con el SDK de SNBC");
        return n;
    }

    private static Err NewErr() => new() { Msg = new byte[96], Res = new byte[128] };
    private static Ctl NewCtl(bool open) => new() { Control = (byte)(open ? 0 : 1), Retry = 2, Res = new byte[64] };

    private void Check(string op, int ret, Err e)
    {
        if (ret == 0) { _log.Info($"SDK {op}: OK"); return; }
        string msg = Encoding.ASCII.GetString(e.Msg ?? []).TrimEnd('\0').Trim();
        _log.Error($"SDK {op}: error 0x{e.Code:X8} {msg}");
        throw new InvalidOperationException($"{op}: el validador devolvió el error 0x{e.Code:X8} {msg}".TrimEnd());
    }

    public GateStatus GetGates()
    {
        var st = new DevStatus { Res = new byte[32] };
        var e = NewErr();
        lock (_sync) Check("Leer estado", GetDevStatus(_h, ref st, ref e), e);
        return new GateStatus(st.Shutter == 1, st.DropDoor == 1, st.Shutter == 2, st.DropDoor == 2,
            st.EntryHasNotes != 0, st.Shutter == 0 && st.EscrowHasNotes != 0, st.ExitHasNotes != 0, st.ChannelHas != 0, st.ProcessStatus);
    }

    public void EscrowGate(bool open)
    {
        var e = NewErr();
        lock (_sync) Check(open ? "Abrir compuerta de escrow" : "Cerrar compuerta de escrow", ShutterControl(_h, NewCtl(open), ref e), e);
    }

    public void DropDoor(bool open)
    {
        var e = NewErr();
        lock (_sync) Check(open ? "Abrir compuerta de caída" : "Cerrar compuerta de caída", DropDoorControl(_h, NewCtl(open), ref e), e);
    }

    // Orden de los campos según CDataStructure.h del SDK V2.7.28.9 (pragma pack 1):
    // tDevStatus 8+32 | tSorterSensorStatus 19+32 | tMachineStatus 18+32 | tExpandSensorStatus 14+26
    private const int OffSorter = 40, OffMachine = 91, OffExpand = 141;
    public static readonly string[] SorterNames =
    [
        "ShutterSensorOpen", "ShutterSensorClose", "DropDoorOpenSensor", "DropDoorCloseSensor", "LockOpenSensor", "LockCloseSensor",
        "TurnRightSensor", "TurnLeftSensor", "ExitCountSensor", "RejectCountSensor", "BackHDSensor", "RejectFullSensor",
        "RejectCheckSensor", "FeedPutSensor", "CISRightSensor", "ThersoldSensor", "CISLeftSensor", "FeedSensor", "ExitCheckSensor",
    ];
    public static readonly string[] MachineNames =
    [
        "MainMotoErr", "FeedMotoErr", "PaperJamErr", "FeedJamErr", "DataCollectJamErr", "TurnJamErr", "OutcashChanJamErr",
        "ElecSwitchErr", "OutcashJamErr", "BackcashJamErr", "CoverupErr", "CISErr", "MagneErr", "ThickErr", "DropDoorErr",
        "ShutterErr", "ShutterLockErr",
    ];
    public static readonly string[] ExpandNames =
    [
        "FishUpBack", "FishUpMiddle", "FishUpLeft", "FishUpRight", "FishUpFrontLeft", "FishUpFrontRight", "FishDownLeft", "FishDownRight",
    ];
    /// <summary>Sensores de gabinete que siguen a los antipesca (SDK V2.7): bolsa en posición, puerta, vibración, bolsa casi llena/llena, traba.</summary>
    public static readonly string[] CabinetNames = ["InPosition", "SafeShutter", "Shake", "AlmostFull", "Full", "ShutterLock"];

    public SensorSnapshot GetSensors()
    {
        var buf = new byte[512]; // la estructura ocupa 181 bytes; se reserva de más por si una versión del SDK la agranda
        var e = NewErr();
        int ret;
        lock (_sync) ret = GetDevStatusService(_h, buf, ref e);
        if (ret != 0) Check("Leer sensores", ret, e);
        var sensors = new Dictionary<string, bool>();
        for (int i = 0; i < SorterNames.Length; i++) sensors[SorterNames[i]] = buf[OffSorter + i] != 0; // 1 = tapado (medido en el equipo; el manual dice lo contrario)
        var errors = new Dictionary<string, bool>();
        for (int i = 0; i < MachineNames.Length; i++) errors[MachineNames[i]] = buf[OffMachine + i] != 0;
        var expand = new Dictionary<string, bool>();
        for (int i = 0; i < ExpandNames.Length; i++) expand[ExpandNames[i]] = buf[OffExpand + i] != 0;
        var cabinet = new Dictionary<string, int>();
        for (int i = 0; i < CabinetNames.Length; i++) cabinet[CabinetNames[i]] = buf[OffExpand + ExpandNames.Length + i];
        var g = new GateStatus(buf[3] == 1, buf[4] == 1, buf[3] == 2, buf[4] == 2, buf[0] != 0, buf[3] == 0 && buf[1] != 0, buf[2] != 0, buf[6] != 0, buf[5]);
        return new SensorSnapshot(g, sensors, errors, expand, buf[OffMachine + MachineNames.Length], DateTime.Now, cabinet);
    }

    public void Dispose()
    {
        if (_h != IntPtr.Zero)
        {
            try { CloseDevice(""); } catch { }
            _h = IntPtr.Zero;
            _log.Info("Mantenimiento directo: validador cerrado");
        }
        try { Environment.CurrentDirectory = _prevDir; } catch { }
    }
}
