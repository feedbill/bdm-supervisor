using System.IO;
using System.IO.Compression;

namespace BdmSupervisor.Services;

/// <summary>Log de la app en archivo diario + buffer en memoria para la vista de Diagnóstico.</summary>
public sealed class LogService
{
    private readonly string _dir;
    private readonly object _lock = new();
    private readonly List<string> _buffer = new();
    public event Action<string>? LineWritten;

    public LogService(string dir)
    {
        _dir = dir;
        Directory.CreateDirectory(dir);
    }

    public string CurrentFile => Path.Combine(_dir, $"supervisor_{DateTime.Now:yyyyMMdd}.log");

    public void Info(string msg) => Write("INFO ", msg);
    public void Warn(string msg) => Write("WARN ", msg);
    public void Error(string msg) => Write("ERROR", msg);

    private void Write(string level, string msg)
    {
        string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {msg}";
        lock (_lock)
        {
            File.AppendAllText(CurrentFile, line + Environment.NewLine);
            _buffer.Add(line);
            if (_buffer.Count > 2000) _buffer.RemoveRange(0, 500);
        }
        LineWritten?.Invoke(line);
    }

    public IReadOnlyList<string> Tail(int n = 300)
    {
        lock (_lock) return _buffer.TakeLast(n).ToList();
    }

    /// <summary>Empaqueta logs de la app + logs del SP + info del sistema en un zip para enviar a Feedbill.</summary>
    public string ExportBundle(string exportDir, string? spLogDir, string deviceInfo)
    {
        Directory.CreateDirectory(exportDir);
        string zip = Path.Combine(exportDir, $"BDM_diag_{Environment.MachineName}_{DateTime.Now:yyyyMMdd_HHmm}.zip");
        using var z = ZipFile.Open(zip, ZipArchiveMode.Create);
        foreach (var f in Directory.GetFiles(_dir, "*.log"))
            z.CreateEntryFromFile(f, "app/" + Path.GetFileName(f));
        if (!string.IsNullOrEmpty(spLogDir) && Directory.Exists(spLogDir))
        {
            foreach (var f in Directory.EnumerateFiles(spLogDir, "*.*", SearchOption.AllDirectories)
                         .Where(f => (DateTime.Now - File.GetLastWriteTime(f)).TotalDays <= 7))
            {
                try { z.CreateEntryFromFile(f, "sp/" + Path.GetRelativePath(spLogDir, f)); } catch { /* archivo en uso */ }
            }
        }
        var e = z.CreateEntry("device_info.txt");
        using (var w = new StreamWriter(e.Open()))
        {
            w.WriteLine($"Equipo: {Environment.MachineName}");
            w.WriteLine($"Usuario: {Environment.UserName}");
            w.WriteLine($"SO: {Environment.OSVersion}");
            w.WriteLine($"Fecha: {DateTime.Now:O}");
            w.WriteLine();
            w.WriteLine(deviceInfo);
        }
        Info($"Bundle de diagnóstico exportado: {zip}");
        return zip;
    }
}
