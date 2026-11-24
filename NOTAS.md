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
- Pedido 18/11 (próxima versión, hecho en el código sin publicar): en Sensores e indicadores, columna derecha "Indicadores luminosos" con cada luz guía listada
  y botones ENCENDER / TITILAR / APAGAR por luz; "Probar todas" sigue disponible. Falta probarlo en el C210T.
- 18/11: prueba de puertos de la placa SIU del C210T (STMIO.ini Board1Port): responde SOLO en COM1 (firmware "JK Firmware:FV2.050.03");
  en COM2–COM6 el SP abre igual pero devuelve todo en 0. Quedó en COM1 (copia: STMIO.ini.bak-antes-prueba-puertos-20261118).
  Con la placa respondiendo: luces guía = 2 (apagadas), proximidad = 2; puertas = 1 → probablemente WFS_SIU_NOT_AVAILABLE (no cableadas/monitoreadas).
- 18/11: etiquetas de conexiones de C210T y BDM-500 en BdmSupervisor/docs. C210T SIU_PHYSICS.ini (índice XFS → pin de la placa):
  luces CARDUNIT(0)=LED03, NOTESDISPENSER(2)=LED02, RECEIPTPRINTER(4)=LED01, CHEQUEUNIT(7)=LED04, BILLACCEPTOR(8)=LED02 (misma que 2);
  sensores OPERATORSWITCH(0)=IN4, SEISMIC(3)=SNS1, PROXIMITY(5)=SNS0; puertas CABINET(0)=SNS4A, SAFE(1)=SNS2A, FRONT(3)=SNS3A, REAR(4)=SNS4A.
  Propuesta: leer SIU_PHYSICS.ini y mostrar solo lo cableado, con el nombre real del equipo (perfil por modelo) y el pin; ocultar luces que comparten LED.
  BDM-500 según etiqueta: IO board en COM1, UPS COM3, lectora COM4; 3 cámaras (cara, entrada de billetes, rechazo), NFC, código de barras,
  2 impresoras y 2 pantallas; sensores de proximidad, vibración, bolsa llena, cerrojo y puerta de caja fuerte, shutter abierto/cerrado,
  bolsa presente, puerta del cabezal; luces de entrada/rechazo/salida de billetes, impresoras 1 y 2, lectora, sin contacto, EPP, huella.
- 0.7.2 (18/11): SIU según SIU_PHYSICS.ini (solo lo cableado, con pin; luces que comparten LED unificadas), nombres reales por modelo
  en appsettings "SiuPinNames", luces con ENCENDER/TITILAR/APAGAR por luz, aviso "el SP responde pero la placa no" si no hay firmware.
- C210T: impresora BT-NH80M configurada en COM5:38400 (estaba en COM2, daba Offline); con COM5 queda en línea, papel bajo, e imprime.
  Valor original guardado en C:\fbtmp\ptr_portdesc_original.txt. Placa SIU confirmada en COM1.
- 0.7.3/0.7.4 (19/11): probado en BDM-500 de Galicia. OK: lectora magnética, NFC, impresora, cámaras (cliente y salida), puertas SIU en vivo.
  Pinpad: GET_DATA era 409 (mal) → 407 en 0.7.4. Código de barras: SP BCR fuera de línea "设备故障" (falla de dispositivo) → revisar driver Superlead/puerto.
  SIU del BDM-500: SIU_PHYSICS.ini solo declara puertas (SNS2A gabinete, SNS4A caja fuerte, IN1 antivandálico); sin luces ni sensores → hay que mapear LEDs (SIUConfig o SNBC).
- 20/11: C210T SIU_PHYSICS.ini completado con la etiqueta + BoardIO.data (tabla "STMIOEV" = placa del C210T):
  NOTESDISPENSER/BILLACCEPTOR=LED02;LIGHTRA (CN30/CN20), SCANNER(12)=LED08 UPS rojo (CN36), GuidLight13=LED06 huella (CN33),
  GuidLight14=LED05 llave mant. (CN34), GuidLight15=LED07 UPS verde (CN35). Backup: SIU_PHYSICS.ini.bak-antes-luces-20261120.
  BDM-500: su placa (FV3.01H, CN distintos) NO está en BoardIO.data → esperar SIU_PHYSICS.ini de SNBC.
- 20/11 (Diego, de memoria, BDM-500 Galicia con SIU_PHYSICS del BDM-100): puerta de caja fuerte OK (SNS4A); el sensor de BOLSA aparece como
  "sísmico" → en ese ini SEISMIC(3)=IN1, o sea bolsa presente = IN1. En el ini original del BDM-500, IN1 figura como VANDALSHIELD (antivandálico): sería la bolsa.
