using BdmSupervisor.Xfs;

namespace BdmSupervisor.Services;

/// <summary>Resultado del conteo de una prueba de depósito.</summary>
public sealed record DepositCount(IReadOnlyList<CimNoteCount> Notes, int Refused);

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

    // Mantenimiento directo (SDK del validador): detiene el SP mientras está activo
    bool NativeActive { get; }
    GateStatus SetEscrowGate(bool open);
    GateStatus SetDropDoor(bool open);
    /// <summary>Aleteo: abre y cierra la compuerta de caída la cantidad de veces indicada.</summary>
    GateStatus TestFlutter(int cycles);
    /// <summary>Lee sensores y errores de máquina con el SDK (entra en mantenimiento directo si hace falta).</summary>
    SensorSnapshot ReadSensors();
    /// <summary>Cierra el SDK y devuelve el equipo al Service Provider.</summary>
    void EndNative();
    /// <summary>CashInStart y espera a que haya billetes en la boca (no da error si está vacía). Devuelve lo contado; el depósito queda abierto.</summary>
    DepositCount CountDeposit(CancellationToken ct, Action<string> progress, int waitSeconds = 120);
    void ConfirmDeposit();     // CashInEnd: los billetes pasan a la bolsa
    void ReturnDeposit();      // CashInRollback: devuelve los billetes
    void Retract();
    void BagExchange();        // StartExchange/EndExchange: cambio de bolsa + reset de contadores
}
