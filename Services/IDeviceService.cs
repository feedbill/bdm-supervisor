using BdmSupervisor.Xfs;

namespace BdmSupervisor.Services;

public enum SupervisorMode { Inactive, Entering, Active, Exiting }

/// <summary>
/// Abstracción del equipo. Dos implementaciones: XfsDeviceService (SP real de SNBC) y MockDeviceService (simulado).
/// Todas las llamadas son bloqueantes; la UI las corre en Task.Run.
/// </summary>
public interface IDeviceService : IDisposable
{
    string Description { get; }
    bool VdmSupported { get; }
    SupervisorMode Mode { get; }

    void Connect();
    CimStatus GetStatus();
    IReadOnlyList<CimCashUnit> GetCashUnits();
    string GetCapabilities();

    /// <summary>Deben llamarse desde el hilo de la UI (manejan mensajes de ventana).</summary>
    Task EnterSupervisorAsync();
    Task ExitSupervisorAsync();
    /// <summary>El SP ya está en modo vendor (nos lanzó él): marcar activo sin pedirlo.</summary>
    void AssumeSupervisorActive();

    // Diagnóstico / técnico (solo con supervisor activo)
    void Reset();
    void OpenShutter();
    void CloseShutter();
    void TestDeposit();        // CashInStart → CashIn → Rollback (devuelve billetes, no deposita)
    void Retract();
    void BagExchange();        // StartExchange/EndExchange: cambio de bolsa + reset de contadores
}
