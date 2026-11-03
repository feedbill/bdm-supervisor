namespace BdmSupervisor.Xfs;

/// <summary>Ventana (HWND) que recibe los mensajes del XFS Manager y los reparte. La implementa la UI (WPF).</summary>
public interface IXfsEventSource
{
    IntPtr Handle { get; }
    /// <summary>(msg, result). El WFSRESULT ya fue copiado; el buffer se libera después del evento.</summary>
    event Action<int, XfsNative.WFSRESULT>? Message;
}
