using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace BdmSupervisor.Services;

/// <summary>Un parámetro del SP de SNBC: dónde vive, su valor actual y una descripción para el técnico.</summary>
public sealed class SpParam
{
    public string Group { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Source { get; init; } = "";      // archivo INI (ruta) o "HKLM32:clave"
    public string Section { get; init; } = "";     // sección INI (vacío para registro)
    public string Key { get; init; } = "";
    public string Value { get; set; } = "";
    public string Original { get; set; } = "";
    public bool Editable { get; init; } = true;
    public bool IsDword { get; init; }
    public bool Changed => Value != Original;
    public string Location => Section.Length > 0 ? $"{Path.GetFileName(Source)} [{Section}]" : Source.Replace(@"HKLM32:SOFTWARE\XFS\SERVICE_PROVIDERS\", "HKLM\\...\\");
}

/// <summary>
/// Lee y escribe los parámetros relevantes del SP XFS de SNBC (SnbcTCR) para el BDM-500:
/// velocidad de conteo, shutters, reintentos de prensado, placa SIU y TestTool del VDM.
/// Antes de escribir un INI se guarda una copia .bak-yyyyMMdd-HHmmss; en el registro, el valor viejo en <nombre>_backup.
/// </summary>
public sealed class SpParamsService
{
    public const string SpDir = @"C:\SNBC SP";
    const string MachineConf = SpDir + @"\SnbcTCR\BNEHostInifile\MachineConf.ini";
    const string BneConfig = SpDir + @"\SnbcTCR\BNEHostInifile\Config.ini";
    const string CrmTcr = SpDir + @"\SnbcTCR\DevCfg\CRM_TCR.ini";
    const string Stmio = SpDir + @"\SIU_SNBC_STMIOAV\STMIO.ini";
    const string TcrCfg = @"HKLM32:SOFTWARE\XFS\SERVICE_PROVIDERS\SnbcTCR\Config";
    const string SiuCfg = @"HKLM32:SOFTWARE\XFS\SERVICE_PROVIDERS\SnbcTCR\SiuConfig";
    public const string VdmCfg = @"HKLM32:SOFTWARE\XFS\SERVICE_PROVIDERS\VDM_SNBC\Config";

    private readonly LogService _log;
    public SpParamsService(LogService log) { _log = log; }

    public bool SpInstalled => Directory.Exists(SpDir);

    public List<SpParam> Load()
    {
        var list = new List<SpParam>
        {
            Ini("Velocidad", "Velocidad de depósito (clasificación)", "Billetes por minuto al depositar con validación. 300–1200 (Galicia: 850)", MachineConf, "MachineSpeedPara", "mSortPPM"),
            Ini("Velocidad", "Velocidad de conteo simple", "Billetes por minuto en conteo sin clasificar. 300–1200 (Galicia: 850)", MachineConf, "MachineSpeedPara", "mCountPPM"),
            Ini("Velocidad", "Velocidad de conteo por denominación", "Billetes por minuto en conteo por denominación. 300–1200", MachineConf, "MachineSpeedPara", "mDenomCountPPM"),

            Ini("Depósito", "Reintentos luego de depósito no exitoso", "Aleteos: veces que reintenta prensar los billetes si el prensado falla (SNBC: retry)", BneConfig, "StoreCash", "retry"),
            Ini("Depósito", "Reintentos luego de depósito exitoso", "Aleteos adicionales después de un prensado exitoso (SNBC: successretry)", BneConfig, "StoreCash", "successretry"),
            Ini("Depósito", "Billete en boca: seguir prensando", "1 = si detecta billete en la boca sigue prensando; 0 = corta con error", BneConfig, "StoreCash", "ExitHasNote"),
            Ini("Depósito", "Máximo de billetes por depósito", "Límite por operación de depósito (CIM MaxCashInItems)", CrmTcr, "PHY", "MaxCashInItems"),
            Ini("Depósito", "Capacidad del escrow", "Billetes en el apilador temporal", CrmTcr, "PHY", "IntermediateStacker"),
            Ini("Depósito", "Moneda", "Código ISO de la moneda del validador (ARS, USD...)", CrmTcr, "PHY", "CurrencyName"),

            Ini("Shutter", "Compuerta validador → bolsa (drop door)", "1 = equipo con compuerta entre validador y bolsa; 0 = sin compuerta (Galicia)", CrmTcr, "PHY", "ShutterDropDoorSurport"),
            Ini("Shutter", "Shutter de entrada", "1 = soporta shutter en la boca de depósito", CrmTcr, "PHY", "ShutterInSurport"),
            Ini("Shutter", "Shutter de salida", "1 = soporta shutter en la salida", CrmTcr, "PHY", "ShutterOutSurport"),
            Ini("Shutter", "Shutter del escrow", "1 = soporta shutter en el apilador temporal", CrmTcr, "PHY", "ShutterCeSurport"),
            Ini("Shutter", "Puerta de bóveda monitoreada", "1 = el SP monitorea la puerta de bóveda; 0 = no", CrmTcr, "PHY", "SaveDoorSurport"),
            Reg("Shutter", "SP controla el shutter", "1 = el SP maneja los shutters; 0 = no", TcrCfg, "SpConTrolShutter"),
            Reg("Shutter", "Shutter de entrada al inicializar", "1 = habilita el shutter de entrada al inicializar", TcrCfg, "IsEnableEntryShutterWhenInitialize"),
            Reg("Shutter", "Shutter de salida al inicializar", "1 = habilita el shutter de salida al inicializar", TcrCfg, "IsEnableExitShutterWhenInitialize"),
            Reg("Shutter", "Abrir shutter después de reset", "1 = abre el shutter al terminar un reset", TcrCfg, "AfterResetOpenShutter"),

            Reg("Placa SIU", "Shutter eléctrico (placa SIU)", "1 = shutter eléctrico controlado por placa SIU (fábrica); 0 = sin shutter eléctrico / mecánico (Galicia)", SiuCfg, "SiuIsControl", dword: true),
            Reg("Placa SIU", "Puerto de la placa SIU", "Puerto serie de la placa SIU", SiuCfg, "SiuPortDesc", editable: false),
            Ini("Placa SIU", "Cantidad de placas", "SNBC indica 1 para configuración sin shutter eléctrico", Stmio, "Board", "BoardCount"),

            Reg("Supervisor (VDM)", "Aplicación de supervisor (TestTool)", "Ejecutable que el SP lanza al entrar en modo supervisor", VdmCfg, "TestTool"),
            Reg("Supervisor (VDM)", "Espera de ACK al entrar (ms)", "0 = sin espera", VdmCfg, "NoACKEnterVDMTime", dword: true),
        };
        foreach (var p in list) { p.Value = Read(p) ?? ""; p.Original = p.Value; }
        return list;
    }

    /// <summary>Guarda los parámetros modificados. Devuelve la lista de cambios aplicados.</summary>
    public List<string> Save(IEnumerable<SpParam> items)
    {
        var done = new List<string>();
        foreach (var p in items.Where(p => p.Changed && p.Editable))
        {
            Write(p);
            done.Add($"{p.Name}: {p.Original} → {p.Value}");
            _log.Info($"Parámetro SP cambiado: {p.Location} {p.Key} = {p.Value} (antes {p.Original})");
            p.Original = p.Value;
        }
        return done;
    }

    public static string ExpectedTestTool(string exePath) => $"{exePath} --vda";

    public string? ReadTestTool() => ReadReg(VdmCfg, "TestTool");

    public void RepairTestTool(string exePath)
    {
        var p = new SpParam { Source = VdmCfg, Key = "TestTool", Value = ExpectedTestTool(exePath), Original = ReadTestTool() ?? "" };
        Write(p);
        _log.Info($"TestTool reparado: {p.Value}");
    }

    // ---- helpers ----
    private static SpParam Ini(string g, string n, string d, string file, string sec, string key, bool editable = true) =>
        new() { Group = g, Name = n, Description = d, Source = file, Section = sec, Key = key, Editable = editable };
    private static SpParam Reg(string g, string n, string d, string regPath, string key, bool editable = true, bool dword = false) =>
        new() { Group = g, Name = n, Description = d, Source = regPath, Key = key, Editable = editable, IsDword = dword };

    private string? Read(SpParam p) => p.Section.Length > 0 ? ReadIni(p.Source, p.Section, p.Key) : ReadReg(p.Source, p.Key);

    private void Write(SpParam p)
    {
        if (p.Section.Length > 0) WriteIni(p.Source, p.Section, p.Key, p.Value);
        else WriteReg(p.Source, p.Key, p.Value, p.IsDword, p.Original);
    }

    private static readonly Encoding IniEnc = Encoding.Latin1;

    public static string? ReadIni(string file, string section, string key)
    {
        if (!File.Exists(file)) return null;
        bool inSec = false;
        foreach (var raw in File.ReadLines(file, IniEnc))
        {
            var line = raw.Trim();
            if (line.StartsWith('[')) { inSec = line.Equals($"[{section}]", StringComparison.OrdinalIgnoreCase); continue; }
            if (!inSec || line.StartsWith(';') || line.StartsWith('#')) continue;
            int eq = line.IndexOf('=');
            if (eq > 0 && line[..eq].Trim().Equals(key, StringComparison.OrdinalIgnoreCase)) return line[(eq + 1)..].Trim();
        }
        return null;
    }

    private void WriteIni(string file, string section, string key, string value)
    {
        var lines = File.ReadAllLines(file, IniEnc).ToList();
        File.Copy(file, $"{file}.bak-{DateTime.Now:yyyyMMdd-HHmmss}", true);
        bool inSec = false, written = false; int secEnd = -1;
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith('['))
            {
                if (inSec && !written) { secEnd = i; break; }
                inSec = line.Equals($"[{section}]", StringComparison.OrdinalIgnoreCase); continue;
            }
            if (!inSec || line.StartsWith(';') || line.StartsWith('#')) continue;
            int eq = line.IndexOf('=');
            if (eq > 0 && line[..eq].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                lines[i] = Regex.Replace(lines[i], @"=\s*.*$", "=" + value); written = true; break;
            }
        }
        if (!written && inSec && secEnd < 0) secEnd = lines.Count;
        if (!written)
        {
            if (secEnd < 0) { lines.Add($"[{section}]"); lines.Add($"{key}={value}"); }
            else lines.Insert(secEnd, $"{key}={value}");
        }
        File.WriteAllLines(file, lines, IniEnc);
    }

    private static RegistryKey? OpenReg(string path, bool writable)
    {
        var sub = path.Replace("HKLM32:", "");
        var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
        return hklm.OpenSubKey(sub, writable);
    }

    public static string? ReadReg(string path, string key)
    {
        using var k = OpenReg(path, false);
        return k?.GetValue(key)?.ToString();
    }

    private static void WriteReg(string path, string key, string value, bool dword, string original)
    {
        using var k = OpenReg(path, true) ?? throw new InvalidOperationException($"No existe la clave {path}");
        if (k.GetValue(key + "_backup") is null) k.SetValue(key + "_backup", original);
        if (dword) k.SetValue(key, int.Parse(value), RegistryValueKind.DWord);
        else k.SetValue(key, value, RegistryValueKind.String);
    }
}
