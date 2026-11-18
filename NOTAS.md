# Notas de la puesta en marcha (PC RECEPCION, 02–03/11/2026)

- Los servicios lógicos XFS del SP de SNBC están en `HKEY_USERS\.DEFAULT\XFS\LOGICAL_SERVICES`, no en HKLM.
- `WFSOpen` desde una sesión SSH (sin escritorio) devuelve `CONNECTION_LOST (-54)`: el SP corre en la sesión interactiva.
- Códigos XFS correctos: -13 DEV_NOT_READY, -48 TIMEOUT, -49 UNSUPP_CATEGORY, -54 CONNECTION_LOST.
- VDM: con `TestTool` inválido el SP responde `DEV_NOT_READY` al `ENTER_MODE_REQ`. Con `TestTool` válido espera el
  `ENTER_MODE_ACK` de cada app registrada; el evento `WFS_SRVE_VDM_ENTER_MODE_REQ` no llega a la ventana registrada
  (el `WFS_EXECUTE_COMPLETE` sí), por eso la app manda el ACK proactivamente 500 ms después del REQ. Funciona.
- El SP emite `fwSafeDoor = 1` diciendo "DOORNOTSUPPORTED" en su log; en el estándar CIM, 1 = DOOROPEN. Consultar a SNBC
  qué hace Softy con ese valor en el BDM-500 sin placa SIU.
- Tester de SNBC (`C:\SNBC SP\VDM\DeviceTestorBySP.exe`): MFC, recursos en chino (GBK). `tools/traduccion-tester-snbc`
  genera la versión en inglés reescribiendo diálogos/strings y parcheando los estados en `.rdata`.
- La app como VDA: `TestTool = <ruta>\BdmSupervisor.exe --vda` (sin comillas; el SP no las entiende).

## Pendiente para la próxima versión (después de 0.7.0) — 18/11/2026
- Hecho en el código, sin publicar:
  - appsettings.json se lee de la carpeta del .exe (antes el single-file usaba la copia embebida: los cambios en la instalación se ignoraban).
  - BDM-500 sin lector de huella.
  - Menú lateral en mayúsculas; seleccionado todo rojo con texto blanco.
  - SIU: SNBC usa 16 luces guía (lpszExtra en 194, no 226); 0 = no existe, 1 = existe sin estado.
- Probado en C210T (Agent detenido unos minutos): SIU en línea con puertas, sensores y 5 luces guía; pinpad XZ-H555 en línea (sin claves cargadas);
  impresora "Offline" (error de hardware, ya conocido); cámaras ambiente y cliente OK.
- Falta: ver el resultado de la prueba de luces (tardó más de 14 s), confirmar que titilan físicamente, y qué significa el valor 1 en puertas/sensores del SIU_STM.
- En el C210T la impresora, el pinpad y la SIU los toma el FeedbillAgent: para probarlos hay que detener el servicio.
