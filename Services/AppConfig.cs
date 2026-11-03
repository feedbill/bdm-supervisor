using System.IO;
using Microsoft.Extensions.Configuration;

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
        };
    }
}
