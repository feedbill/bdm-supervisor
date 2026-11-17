# BDM-500 Supervisor (Feedbill) — v0.1 esqueleto

App Windows (C# / .NET 8 / WPF) para el depositario SNBC BDM-500 (mismo core que el BDM-100):
entra en **modo supervisor** (XFS VDM) y ofrece **herramientas de diagnóstico** sobre el Service Provider XFS ya instalado.

## Cómo compilar

1. Instalar **.NET 8 SDK** (x86 o x64, da igual: el proyecto compila a x86).
2. Doble clic en `build.cmd` → genera `publish\BdmSupervisor.exe`.
   (O abrir `BdmSupervisor.csproj` con Visual Studio 2022 y compilar en Release.)

## Cómo probar sin equipo (modo simulador)

`appsettings.json` viene con `"Mode": "Mock"`. Ejecutar el .exe y recorrer las 4 pestañas:
Estado · Supervisor (PIN por defecto `1234`) · Diagnóstico · Mantenimiento.

## Cómo conectar al BDM real

1. En el equipo, abrir `regedit` y mirar `HKLM\SOFTWARE\XFS\LOGICAL_SERVICES`
   (en Windows 64 bits suele estar en `HKLM\SOFTWARE\WOW6432Node\XFS\LOGICAL_SERVICES`).
   Ahí aparecen los **nombres lógicos** que registró el SP de SNBC (ej. `CashInModule`, `CIM`, `SNBC_CIM`, etc.).
2. Poner ese nombre en `Device.CimLogicalName` y cambiar `Device.Mode` a `"Xfs"`.
3. Si SNBC registró también un servicio de clase **VDM**, poner su nombre en `Device.VdmLogicalName`.
   Si no existe, la app lo detecta y avisa: en ese caso hay que **cerrar Softy** antes de entrar en supervisor
   (dos apps no pueden tener el dispositivo a la vez).
4. Ajustar `Paths.SpLogDir` (logs del SP) y `Paths.SnbcToolPath` (PC tool del validador BNE) si existen.

La app corre **elevada** (manifest `requireAdministrator`) porque en sucursal necesita leer los logs del SP.

## Qué hace cada pestaña

| Pestaña | Función | Comandos XFS |
|---|---|---|
| Estado | Semáforo por módulo, unidades (bolsa/reject) con contadores, firmware/template (`lpszExtra`) | `WFS_INF_CIM_STATUS`, `CASH_UNIT_INFO` |
| Supervisor | Entrar/salir del modo supervisor con PIN | `WFS_CMD_VDM_ENTER_MODE_REQ` / `EXIT_MODE_REQ` |
| Diagnóstico | Reset, abrir/cerrar shutter, test de depósito (cuenta y **devuelve**), retract a bolsa, cambio de bolsa + reset de contadores, capabilities, log en vivo | `RESET`, `OPEN/CLOSE_SHUTTER`, `CASH_IN_START/CASH_IN/ROLLBACK`, `RETRACT`, `START/END_EXCHANGE` |
| Mantenimiento | Exportar ZIP de diagnóstico (logs app + logs SP + info del equipo), abrir carpeta de logs, lanzar el tool SNBC del validador | — |

## Estructura

```
Xfs/XfsNative.cs      P/Invoke a msxfs.dll (XFS Manager 3.x)
Xfs/XfsConstants.cs   IDs de comandos/estados CIM y VDM, códigos de error
Xfs/XfsSession.cs     StartUp/Open/GetInfo/Execute/Close
Xfs/CimService.cs     Parseo de WFSCIMSTATUS / cash units + comandos de técnico
Xfs/VdmService.cs     Enter/Exit vendor dependent mode
Services/             IDeviceService (contrato), XfsDeviceService (real), MockDeviceService (simulador), LogService, AppConfig
MainWindow.xaml(.cs)  UI de 4 pestañas
```

## Pendientes que dependen de SNBC

- Confirmar nombres lógicos del SP y si implementa la clase **VDM**.
- Confirmar que el layout de `WFSCIMSTATUS` / `WFSCIMCASHIN` del SP es el estándar CIM 3.x
  (si el SP devuelve `lpszExtra` con firmware/template, ya se muestra en Estado).
- Ruta y modo de invocación del PC tool del validador BNE (firmware, templates, calibración).
- Si el SP emite eventos de servicio (puerta abierta, bolsa llena, fraude), agregar `WFSRegister` + ventana receptora
  de mensajes WFS_SERVICE_EVENT para mostrarlos en tiempo real (hoy la app es polling con el botón Actualizar).

## Roadmap sugerido

v0.2 · eventos XFS en tiempo real + auto-refresh · v0.3 · envío del bundle de diagnóstico a Feedbill Cloud · v0.4 · perfiles de usuario (custodio / técnico Feedbill) con permisos distintos.

## Cambios 0.2.x (17/11/2026)

- **Compuertas y aleteo** (pestaña Supervisor): abrir/cerrar compuerta de escrow, abrir/cerrar compuerta de caída y prueba de aleteo
  (ciclos de la compuerta de caída = 1 + `successretry` de `Config.ini [StoreCash]`). Se hacen con el SDK del validador
  (`C:\SNBC SP\SnbcTCR\BNEHostDll.dll`: `ShutterControl` = escrow, `DropDoorControl` = caída; 0 abrir, 1 cerrar), por eso
  la app detiene el SP (`SNBC_SPCRM.exe`) mientras se usan. «Volver a operación normal» o salir del supervisor lo devuelve al SP.
- **Prueba de depósito**: si la boca está vacía pide los billetes (2 min, cancelable); muestra el conteo por denominación y deja
  elegir depositar (CashInEnd) o devolver (Rollback). CashInStart con entrada `POSINCENTER`.
- **Estado**: fila «Último error del validador» y aviso de atasco si el SP lo informa.
- Se quitaron: botones de shutter, retract a bolsa y edición de parámetros de depósito.
- Logs del SP leídos de la carpeta configurada en el propio SP (`HKLM\...\SNBC\SpLog\LogPath`, hoy `C:\snbclogs`).
- Probado en PC de recepción con BNE-S110M y SP XFS BDM-500 v2.0.0.1 (SnbcTCR 2.0.2.16) + VDM 2.0.0.4.
