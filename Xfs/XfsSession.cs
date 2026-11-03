using System.Runtime.InteropServices;
using BdmSupervisor.Services;

namespace BdmSupervisor.Xfs;

public sealed class XfsException(int hResult, string context)
    : Exception($"{context}: {XfsConst.ErrorName(hResult)} ({hResult})")
{
    public int HResult2 { get; } = hResult;
}

/// <summary>
/// Maneja WFSStartUp/CleanUp y la apertura de un servicio lógico (CIM o VDM).
/// Un XfsSession por servicio lógico. Thread-safe para uso simple desde la UI.
/// </summary>
public sealed class XfsSession : IDisposable
{
    private static int _startupCount;
    private static readonly object StartupLock = new();

    private readonly LogService _log;
    private readonly uint _timeout;
    private ushort _hService;
    private IntPtr _hApp;

    public string LogicalName { get; }
    public bool IsOpen => _hService != 0;
    public ushort HService => _hService;
    public string SpDescription { get; private set; } = "";
    public string SpVersion { get; private set; } = "";

    public XfsSession(string logicalName, string appId, uint timeoutMs, uint traceLevel, LogService log)
    {
        LogicalName = logicalName;
        _log = log;
        _timeout = timeoutMs;

        lock (StartupLock)
        {
            if (_startupCount == 0)
            {
                var ver = new XfsNative.WFSVERSION();
                int hr = XfsNative.WFSStartUp(XfsNative.API_VERSIONS_REQUIRED, ref ver);
                if (hr != XfsConst.WFS_SUCCESS && hr != XfsConst.WFS_ERR_ALREADY_STARTED)
                    throw new XfsException(hr, "WFSStartUp");
                _log.Info($"XFS Manager iniciado: {ver.szDescription} v{ver.wVersion:X4}");
            }
            _startupCount++;
        }

        XfsNative.WFSCreateAppHandle(out _hApp);

        var srv = new XfsNative.WFSVERSION();
        var spi = new XfsNative.WFSVERSION();
        int hrOpen = XfsNative.WFSOpen(logicalName, _hApp, appId, traceLevel, timeoutMs,
            XfsNative.VERSIONS_REQUIRED, ref srv, ref spi, out _hService);
        if (hrOpen != XfsConst.WFS_SUCCESS)
            throw new XfsException(hrOpen, $"WFSOpen({logicalName})");

        SpDescription = srv.szDescription;
        SpVersion = $"{srv.wVersion >> 8}.{srv.wVersion & 0xFF:D2}";
        _log.Info($"Servicio '{logicalName}' abierto (hService={_hService}) SP: {SpDescription} v{SpVersion}");
    }

    /// <summary>WFSGetInfo síncrono. Devuelve el WFSRESULT; el llamador debe liberar con FreeResult.</summary>
    public IntPtr GetInfo(uint category, IntPtr query = default)
    {
        int hr = XfsNative.WFSGetInfo(_hService, category, query, _timeout, out var result);
        if (hr != XfsConst.WFS_SUCCESS)
        {
            if (result != IntPtr.Zero) XfsNative.WFSFreeResult(result);
            throw new XfsException(hr, $"WFSGetInfo({category})");
        }
        return result;
    }

    /// <summary>WFSExecute síncrono. Devuelve el WFSRESULT; el llamador debe liberar con FreeResult.</summary>
    public IntPtr Execute(uint command, IntPtr cmdData = default, uint? timeoutMs = null)
    {
        _log.Info($"[{LogicalName}] Execute {command}");
        int hr = XfsNative.WFSExecute(_hService, command, cmdData, timeoutMs ?? _timeout, out var result);
        if (hr != XfsConst.WFS_SUCCESS)
        {
            if (result != IntPtr.Zero) XfsNative.WFSFreeResult(result);
            _log.Error($"[{LogicalName}] Execute {command} → {XfsConst.ErrorName(hr)}");
            throw new XfsException(hr, $"WFSExecute({command})");
        }
        return result;
    }

    public static XfsNative.WFSRESULT ReadResult(IntPtr lpResult) =>
        Marshal.PtrToStructure<XfsNative.WFSRESULT>(lpResult);

    public static void FreeResult(IntPtr lpResult)
    {
        if (lpResult != IntPtr.Zero) XfsNative.WFSFreeResult(lpResult);
    }

    public void Register(IntPtr hWnd, uint eventClasses)
    {
        int hr = XfsNative.WFSRegister(_hService, eventClasses, hWnd);
        if (hr != XfsConst.WFS_SUCCESS) throw new XfsException(hr, "WFSRegister");
    }

    public void Dispose()
    {
        if (_hService != 0)
        {
            XfsNative.WFSClose(_hService);
            _log.Info($"Servicio '{LogicalName}' cerrado");
            _hService = 0;
        }
        if (_hApp != IntPtr.Zero)
        {
            XfsNative.WFSDestroyAppHandle(_hApp);
            _hApp = IntPtr.Zero;
        }
        lock (StartupLock)
        {
            _startupCount--;
            if (_startupCount == 0) XfsNative.WFSCleanUp();
        }
    }
}
