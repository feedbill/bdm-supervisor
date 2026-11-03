# BDM-500 Supervisor (Feedbill)

Aplicación Windows (C# / .NET 8 / WPF, x86) para el depositario **SNBC BDM-500** (mismo core que el BDM-100):
entra y sale del **modo supervisor** (XFS VDM), muestra el estado del equipo, ofrece herramientas de
diagnóstico y permite ver/editar los parámetros del Service Provider XFS de SNBC.

Validada el 03/11/2026 contra el SP real de SNBC (SP V2.0.1.2, SnbcTCR V2.0.3.1, SDK BNE 2.7.28.9) con un BNE-S110M.

## Cómo funciona con el SP de SNBC

| Qué | Dónde |
|---|---|
| Servicio CIM | nombre lógico `CashAcceptor` (provider `SnbcTCR`) en `HKEY_USERS\.DEFAULT\XFS\LOGICAL_SERVICES` |
| Servicio VDM | nombre lógico `VDM30` (provider `VDM_SNBC`) |
| App que el SP lanza al entrar en VDM | `HKLM\SOFTWARE\WOW6432Node\XFS\SERVICE_PROVIDERS\VDM_SNBC\Config\TestTool` → `BdmSupervisor.exe --vda` |
| Protocolo VDM | `WFSRegister` + `WFSAsyncExecute(ENTER_MODE_REQ)` + `ENTER_MODE_ACK` proactivo (el evento `ENTER_MODE_REQ` del SP no llega a la ventana registrada; pendiente de analizar) |
| Versiones XFS pedidas | `dwSrvcVersionsRequired = 0x1E030003` (3.00–3.30), `WFSStartUp 0x00030003` |

La app es de **instancia única**: si el SP la lanza como TestTool mientras ya está abierta, solo la trae al frente.
Con `--vda` (lanzada por el SP desde la app bancaria) arranca con el supervisor ya activo, en la pestaña Diagnóstico.
Al arrancar verifica que `TestTool` apunte a sí misma y ofrece **Reparar** si SNBC lo pisó con una actualización.

## Pestañas

- **Estado**: módulos (sin los `NOTSUPP`), unidades de efectivo, firmware/SP (`lpszExtra`). La puerta de bóveda se muestra
  "No monitoreada" salvo `Device.SafeDoorMonitored=true` (el SP envía `fwSafeDoor=1` aunque no tenga sensor; ver NOTAS).
- **Supervisor**: PIN + entrar/salir del VDM.
- **Diagnóstico**: reset, shutter, test de depósito con rollback, retract, cambio de bolsa, capabilities, log en vivo.
- **Parámetros**: velocidad de conteo, reintentos de prensado ("aleteo"), shutters, placa SIU, TestTool. Edición solo con
  supervisor activo, con backup (`.bak-fecha` / `<valor>_backup`). Botón **Perfil Galicia** (sin shutter eléctrico) y **Reiniciar SP**.
- **Mantenimiento**: bundle ZIP de logs (app + `D:\snbclogs`), carpeta de logs, lanzar el tester de SNBC.

## Perfiles de equipo (parámetros del SP)

| Parámetro | Sin shutter eléctrico (**Galicia**) | Con shutter eléctrico (fábrica) |
|---|---|---|
| `HKLM\...\SnbcTCR\SiuConfig\SiuIsControl` | 0 | 1 |
| `SIU_SNBC_STMIOAV\STMIO.ini [Board] BoardCount` | 1 | 1 |
| `SnbcTCR\DevCfg\CRM_TCR.ini [PHY] ShutterDropDoorSurport` | 0 | 1 |

Con `SiuIsControl=1` y sin placa SIU el SP queda en `HWERROR` (`SafeShutter status error`, `STMIO_Open COM1`).

## Compilar

.NET 8 SDK → `build.cmd` (publica x86 autocontenido en `publish\`). `check.cmd` compila y corre `XfsCheck`.
Instalador: Inno Setup 6 → `installer\BdmSupervisor.iss` → `installer\BdmSupervisor-Setup-x.y.z.exe`
(tareas: acceso directo, registrar TestTool, perfil con/sin shutter eléctrico; restaura todo al desinstalar).

## Estructura

```
Xfs/            P/Invoke a msxfs.dll, constantes CIM/VDM, sesión, CimService, VdmService (protocolo VDM)
Services/       IDeviceService (Xfs real / Mock), LogService, AppConfig, SpParamsService (INI + registro del SP)
XfsEventWindow  ventana message-only que recibe los mensajes del XFS Manager
XfsCheck/       consola de diagnóstico: registro XFS, SP, msxfs.dll, WFSOpen + STATUS
installer/      script Inno Setup
tools/traduccion-tester-snbc/  script que genera DeviceTestorBySP_EN.exe (tester de SNBC traducido al inglés)
```
