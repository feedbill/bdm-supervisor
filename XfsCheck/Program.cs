using System.IO;
using BdmSupervisor.Services;
using BdmSupervisor.Xfs;
using Microsoft.Win32;

// Uso:  XfsCheck.exe                → lista servicios registrados y prueba todos los CIM
//       XfsCheck.exe <NombreLogico> → prueba solo ese servicio
// Cada paso imprime OK / FALLO con el error XFS, para saber dónde se corta.

Console.OutputEncoding = System.Text.Encoding.UTF8;
var log = new LogService(Path.Combine(AppContext.BaseDirectory, "logs"));

Console.WriteLine("== 1. Registro XFS ==");
var services = new List<(string name, string provider, string cls)>();
// XFS 3.x registra los servicios lógicos en HKEY_USERS\.DEFAULT\XFS (y a veces en HKCU o HKLM)
var roots = new (RegistryKey hive, string label, string path)[]
{
    (Registry.Users, "HKU\\.DEFAULT", @".DEFAULT\XFS\LOGICAL_SERVICES"),
    (Registry.CurrentUser, "HKCU", @"XFS\LOGICAL_SERVICES"),
    (Registry.LocalMachine, "HKLM", @"SOFTWARE\XFS\LOGICAL_SERVICES"),
};
foreach (var (hive, label, root) in roots)
{
    using var k = hive.OpenSubKey(root);
    if (k is null) { Console.WriteLine($"  (no existe {label}\\{root})"); continue; }
    foreach (var name in k.GetSubKeyNames())
    {
        using var s = k.OpenSubKey(name)!;
        string provider = s.GetValue("provider")?.ToString() ?? "?";
        string cls = s.GetValue("class")?.ToString() ?? "?";
        Console.WriteLine($"  servicio '{name}'  class={cls}  provider={provider}");
        services.Add((name, provider, cls));
    }
}
if (services.Count == 0) { Console.WriteLine("  FALLO: no hay servicios lógicos registrados. El SP no está instalado."); return 1; }

Console.WriteLine("\n== 2. Service Providers registrados ==");
foreach (var root in new[] { @"SOFTWARE\WOW6432Node\XFS\SERVICE_PROVIDERS", @"SOFTWARE\XFS\SERVICE_PROVIDERS" })
{
    using var k = Registry.LocalMachine.OpenSubKey(root);
    if (k is null) continue;
    foreach (var name in k.GetSubKeyNames())
    {
        using var s = k.OpenSubKey(name)!;
        string dll = s.GetValue("dllname")?.ToString() ?? "?";
        bool exists = File.Exists(dll) || File.Exists(Path.Combine(Environment.SystemDirectory, dll))
                      || File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SysWOW64", dll));
        Console.WriteLine($"  SP '{name}'  dll={dll}  {(exists ? "OK existe" : "FALLO: no se encuentra la DLL")}");
    }
}

Console.WriteLine("\n== 3. msxfs.dll ==");
string sysWow = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SysWOW64", "msxfs.dll");
string sys32 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "msxfs.dll");
Console.WriteLine($"  SysWOW64: {(File.Exists(sysWow) ? "OK" : "no")}   System32: {(File.Exists(sys32) ? "OK" : "no")}");
if (!File.Exists(sysWow) && !File.Exists(sys32)) { Console.WriteLine("  FALLO: falta el XFS Manager."); return 1; }

var targets = args.Length > 0 ? services.Where(s => s.name.Equals(args[0], StringComparison.OrdinalIgnoreCase)).ToList()
                              : services.Where(s => s.cls.Equals("CIM", StringComparison.OrdinalIgnoreCase) || s.cls == "13").ToList();
if (targets.Count == 0) targets = services; // si no sabemos la clase, probamos todos

int rc = 0;
foreach (var t in targets)
{
    Console.WriteLine($"\n== 4. Abrir '{t.name}' ==");
    XfsSession? session = null;
    try
    {
        session = new XfsSession(t.name, "XfsCheck", 30000, 0, log);
        Console.WriteLine($"  OK WFSOpen → SP: {session.SpDescription} v{session.SpVersion}");
    }
    catch (Exception ex) { Console.WriteLine($"  FALLO WFSOpen: {ex.Message}"); rc = 1; continue; }

    try
    {
        var cim = new CimService(session, log);
        var st = cim.GetStatus();
        Console.WriteLine($"  OK STATUS → Device={st.Device} BNR={st.BanknoteReader} Acceptor={st.Acceptor} Stacker={st.IntermediateStacker} SafeDoor={st.SafeDoor}");
        foreach (var p in st.Positions) Console.WriteLine($"     pos {p.Position}: shutter={p.Shutter} transport={p.Transport}");
        if (st.Extra.Length > 0) Console.WriteLine($"     extra: {st.Extra}");
        try
        {
            foreach (var u in cim.GetCashUnits())
                Console.WriteLine($"  OK CASH_UNIT #{u.Number} {u.Type} {u.UnitId} count={u.Count}/{u.Maximum} {u.Status}");
        }
        catch (Exception ex) { Console.WriteLine($"  aviso CASH_UNIT_INFO: {ex.Message}"); }
        try { Console.WriteLine($"  OK CAPS → {cim.GetCapabilitiesRaw()}"); }
        catch (Exception ex) { Console.WriteLine($"  aviso CAPABILITIES: {ex.Message}"); }
        if (st.Device != "ONLINE") { Console.WriteLine($"  ATENCIÓN: el equipo no está ONLINE (está {st.Device}). Revisar cable/driver/puerto COM en la config del SP."); rc = 2; }
    }
    catch (Exception ex)
    {
        // Si no es CIM (p. ej. VDM), el GetInfo 1301 da UNSUPP_CATEGORY: probamos como VDM
        Console.WriteLine($"  STATUS CIM falló ({ex.Message}); probando como VDM...");
        try { Console.WriteLine($"  OK VDM STATUS → {new VdmService(session, log).GetStatus()}"); }
        catch (Exception ex2) { Console.WriteLine($"  FALLO: {ex2.Message}"); rc = 1; }
    }
    finally { session.Dispose(); }
}

Console.WriteLine(rc == 0 ? "\n>> XFS OK: comunicación con el equipo verificada." : "\n>> Hubo fallos; mandá esta salida completa a Feedbill/SNBC.");
return rc;
