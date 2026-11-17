using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace BdmSupervisor.Services;

/// <summary>Estado de compuertas y sensores leído directo del validador (SDK BNEHostDll, GetDevStatus).</summary>
public sealed record GateStatus(bool EscrowOpen, bool DropDoorOpen, bool EscrowFault, bool DropDoorFault,
    bool NotesInEntry, bool NotesInEscrow, bool NotesInReject, bool NotesInTransport, int Process);

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
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int ShutterControl(IntPtr h, Ctl c, ref Err e);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int DropDoorControl(IntPtr h, Ctl c, ref Err e);

    private IntPtr _h;
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
        Check("Leer estado", GetDevStatus(_h, ref st, ref e), e);
        return new GateStatus(st.Shutter == 1, st.DropDoor == 1, st.Shutter == 2, st.DropDoor == 2,
            st.EntryHasNotes != 0, st.Shutter == 0 && st.EscrowHasNotes != 0, st.ExitHasNotes != 0, st.ChannelHas != 0, st.ProcessStatus);
    }

    public void EscrowGate(bool open)
    {
        var e = NewErr();
        Check(open ? "Abrir compuerta de escrow" : "Cerrar compuerta de escrow", ShutterControl(_h, NewCtl(open), ref e), e);
    }

    public void DropDoor(bool open)
    {
        var e = NewErr();
        Check(open ? "Abrir compuerta de caída" : "Cerrar compuerta de caída", DropDoorControl(_h, NewCtl(open), ref e), e);
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
