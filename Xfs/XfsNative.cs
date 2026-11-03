using System.Runtime.InteropServices;

namespace BdmSupervisor.Xfs;

/// <summary>
/// P/Invoke al XFS Manager (msxfs.dll). CEN/XFS 3.x.
/// IMPORTANTE: msxfs.dll es de 32 bits en casi todos los equipos → compilar la app como x86.
/// Todas las estructuras XFS van con Pack = 1.
/// </summary>
public static class XfsNative
{
    private const string Dll = "msxfs.dll";

    // Versiones XFS 3.x: 0x0003 (3.00) ... se piden en formato (low << 16) | high
    public const uint XFS_VERSION_3_00 = 0x00000003;
    public const uint XFS_VERSION_3_30 = 0x00001E03; // 3.30
    public const uint VERSIONS_REQUIRED = (XFS_VERSION_3_30 << 16) | XFS_VERSION_3_00; // high word = máxima, low word = mínima
    public const uint API_VERSIONS_REQUIRED = (XFS_VERSION_3_00 << 16) | XFS_VERSION_3_00; // WFSStartUp: 3.00 exacto

    public const uint WFS_INDEFINITE_WAIT = 0;

    // Mensajes de ventana (WM_USER = 0x0400)
    public const int WFS_OPEN_COMPLETE = 0x0401;
    public const int WFS_CLOSE_COMPLETE = 0x0402;
    public const int WFS_REGISTER_COMPLETE = 0x0405;
    public const int WFS_GETINFO_COMPLETE = 0x0407;
    public const int WFS_EXECUTE_COMPLETE = 0x0408;
    public const int WFS_EXECUTE_EVENT = 0x0409;
    public const int WFS_SERVICE_EVENT = 0x040A;
    public const int WFS_USER_EVENT = 0x040B;
    public const int WFS_SYSTEM_EVENT = 0x040C;

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct WFSVERSION
    {
        public ushort wVersion;
        public ushort wLowVersion;
        public ushort wHighVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szDescription;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szSystemStatus;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SYSTEMTIME
    {
        public ushort wYear, wMonth, wDayOfWeek, wDay, wHour, wMinute, wSecond, wMilliseconds;
        public DateTime ToDateTime() =>
            wYear == 0 ? DateTime.MinValue : new DateTime(wYear, wMonth, wDay, wHour, wMinute, wSecond, wMilliseconds);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct WFSRESULT
    {
        public uint RequestID;
        public ushort hService;
        public SYSTEMTIME tsTimestamp;
        public int hResult;
        public uint dwCommandCode; // union con dwEventID
        public IntPtr lpBuffer;
    }

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    public static extern int WFSStartUp(uint dwVersionsRequired, ref WFSVERSION lpWFSVersion);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    public static extern int WFSCleanUp();

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public static extern int WFSOpen(
        string lpszLogicalName, IntPtr hApp, string lpszAppID, uint dwTraceLevel, uint dwTimeOut,
        uint dwSrvcVersionsRequired, ref WFSVERSION lpSrvcVersion, ref WFSVERSION lpSPIVersion,
        out ushort lphService);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    public static extern int WFSClose(ushort hService);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    public static extern int WFSRegister(ushort hService, uint dwEventClass, IntPtr hWndReg);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    public static extern int WFSDeregister(ushort hService, uint dwEventClass, IntPtr hWndReg);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    public static extern int WFSGetInfo(ushort hService, uint dwCategory, IntPtr lpQueryDetails, uint dwTimeOut, out IntPtr lppResult);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    public static extern int WFSExecute(ushort hService, uint dwCommand, IntPtr lpCmdData, uint dwTimeOut, out IntPtr lppResult);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    public static extern int WFSFreeResult(IntPtr lpResult);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    public static extern int WFSAsyncExecute(ushort hService, uint dwCommand, IntPtr lpCmdData, uint dwTimeOut, IntPtr hWnd, out uint lpRequestID);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    public static extern int WFSLock(ushort hService, uint dwTimeOut, out IntPtr lppResult);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    public static extern int WFSUnlock(ushort hService);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    public static extern int WFSCreateAppHandle(out IntPtr lphApp);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    public static extern int WFSDestroyAppHandle(IntPtr hApp);
}
