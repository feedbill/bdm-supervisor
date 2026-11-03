using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace BdmSupervisor;

public partial class App : Application
{
    public static bool LaunchedBySp { get; private set; }
    private static Mutex? _mutex;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string? cls, string title);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int cmd);

    protected override void OnStartup(StartupEventArgs e)
    {
        LaunchedBySp = e.Args.Any(a => a.Equals("--vda", StringComparison.OrdinalIgnoreCase));

        _mutex = new Mutex(true, "Feedbill.BdmSupervisor.SingleInstance", out bool first);
        if (!first)
        {
            // Ya hay una instancia (p. ej. el SP nos lanzÃ³ como TestTool mientras la app ya estaba abierta): traerla al frente y salir
            var h = FindWindow(null, BdmSupervisor.MainWindow.WindowTitle);
            if (h != IntPtr.Zero) { ShowWindow(h, 9 /*SW_RESTORE*/); SetForegroundWindow(h); }
            Shutdown();
            return;
        }

        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.Message, "BDM Supervisor â€” error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
    }
}

