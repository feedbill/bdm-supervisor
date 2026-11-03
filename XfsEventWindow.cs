using System.Runtime.InteropServices;
using System.Windows.Interop;
using BdmSupervisor.Xfs;

namespace BdmSupervisor;

/// <summary>Ventana message-only que recibe los mensajes del XFS Manager (WFSRegister / WFSAsyncExecute).</summary>
public sealed class XfsEventWindow : IXfsEventSource, IDisposable
{
    private readonly HwndSource _src;
    public IntPtr Handle => _src.Handle;
    public event Action<int, XfsNative.WFSRESULT>? Message;

    public XfsEventWindow()
    {
        var p = new HwndSourceParameters("FeedbillXfsEvents") { Width = 0, Height = 0, WindowStyle = 0, ParentWindow = new IntPtr(-3) /* HWND_MESSAGE */ };
        _src = new HwndSource(p);
        _src.AddHook(Hook);
    }

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg >= XfsNative.WFS_OPEN_COMPLETE && msg <= XfsNative.WFS_SYSTEM_EVENT && lParam != IntPtr.Zero)
        {
            var r = Marshal.PtrToStructure<XfsNative.WFSRESULT>(lParam);
            try { Message?.Invoke(msg, r); }
            finally { XfsNative.WFSFreeResult(lParam); }
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose() { _src.RemoveHook(Hook); _src.Dispose(); }
}
