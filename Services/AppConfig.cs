using System.IO;
using Microsoft.Extensions.Configuration;
using BdmSupervisor.Xfs;

namespace BdmSupervisor.Services;

public sealed class AppConfig
{
    public string Mode { get; init; } = "Mock";
    public string CimLogicalName { get; init; } = "CashInModule";
    public string VdmLogicalName { get; init; } = "VDM";
    public string AppId { get; init; } = "FeedbillBdmSupervisor";
    public uint TimeoutMs { get; init; } = 30000;
    public uint TraceLevel { get; init; } = 0;
    /// <summary>false = el SP no monitorea la puerta de bóveda (SaveDoorSurport=0): se muestra "No monitoreada" en vez del valor XFS.</summary>
    public bool SafeDoorMonitored { get; init; } = false;

    public string LogDir { get; init; } = @"C:\Feedbill\BdmSupervisor\logs";
    public string ExportDir { get; init; } = @"C:\Feedbill\BdmSupervisor\export";
    public string? SpLogDir { get; init; }
    public string? SnbcToolPath { get; init; }
    public string SupervisorPin { get; init; } = "1234";
    /// <summary>Contraseña para modificar los parámetros del SP desde la pestaña "Parámetros SP".</summary>
    public string ParamsPassword { get; init; } = "1234";
    /// <summary>Carpeta donde el SP de cámaras guarda las fotos (opcional; si no, se buscan en las carpetas habituales de SNBC).</summary>
    public string? CameraImageDir { get; init; }
    /// <summary>Modelo del equipo (encabezado) y módulos XFS que tiene: una pestaña por módulo.</summary>
    public string Model { get; init; } = "BDM-500";
    public List<ModuleDef> Modules { get; init; } = DefaultModules;

    /// <summary>Módulos del BDM-500 (servicios lógicos del SP de SNBC). Se usan si appsettings.json no trae la sección "Modules".</summary>
    public static readonly List<ModuleDef> DefaultModules =
    [
        new("PTR", "ReceiptPrinter", "Impresora de tickets"),
        new("IDC", "CardReaderEx", "Lector de tarjeta magnética"),
        new("NFC", "ICCardReader", "Lector NFC"),
        new("PIN", "Encryptor", "Teclado pinpad"),
        new("BCR", "BarcodeReader", "Lector de código de barras"),
        new("CAM", "Camera", "Cámaras"),
        new("FINGER", "FingerScanner", "Lector de huella"),
        new("SIU", "SIU", "Sensores e indicadores"),
    ];

    public static AppConfig Load()
    {
        var root = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .Build();
        var d = root.GetSection("Device");
        var p = root.GetSection("Paths");
        return new AppConfig
        {
            Mode = d["Mode"] ?? "Mock",
            CimLogicalName = d["CimLogicalName"] ?? "CashInModule",
            VdmLogicalName = d["VdmLogicalName"] ?? "VDM",
            AppId = d["AppId"] ?? "FeedbillBdmSupervisor",
            TimeoutMs = uint.TryParse(d["TimeoutMs"], out var t) ? t : 30000,
            TraceLevel = uint.TryParse(d["TraceLevel"], out var tl) ? tl : 0,
            SafeDoorMonitored = bool.TryParse(d["SafeDoorMonitored"], out var sdm) && sdm,
            LogDir = p["LogDir"] ?? @"C:\Feedbill\BdmSupervisor\logs",
            ExportDir = p["ExportDir"] ?? @"C:\Feedbill\BdmSupervisor\export",
            SpLogDir = p["SpLogDir"],
            SnbcToolPath = p["SnbcToolPath"],
            SupervisorPin = root.GetSection("Supervisor")["Pin"] ?? "1234",
            ParamsPassword = root.GetSection("Supervisor")["ParamsPassword"] ?? "1234",
            CameraImageDir = p["CameraImageDir"],
            Model = d["Model"] ?? "BDM-500",
            Modules = root.GetSection("Modules").Exists()
                ? root.GetSection("Modules").GetChildren()
                    .Where(m => !string.Equals(m["Enabled"], "false", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(m["Type"]) && !string.IsNullOrEmpty(m["LogicalName"]))
                    .Select(m => new ModuleDef(m["Type"]!, m["LogicalName"]!, m["Title"] ?? m["LogicalName"]!)).ToList()
                : DefaultModules,
        };
    }
}
