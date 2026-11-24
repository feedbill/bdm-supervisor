# ESTADO — BDM Supervisor (app de modo supervisor para depositarios SNBC)

Última actualización: 24/11/2026
Regla: este archivo es la fuente de verdad del proyecto. Cada sesión de trabajo empieza leyéndolo y termina actualizándolo.
El detalle técnico de cada hallazgo está en NOTAS.md.

## Qué es
App Windows (C# / .NET) pedida por Banco Galicia para el BDM-500. Activa el modo supervisor del equipo por XFS (VDM) y da herramientas de diagnóstico de cada dispositivo, para que el banco no tenga que desarrollarlas.

## Dónde estamos
- Versión publicada: 0.7.4 (19/11/2026), probada en el BDM-500 del laboratorio de Galicia.
- Funciona en el BDM-500: lectora magnética, NFC, impresora, cámaras (cliente y salida) y puertas de la placa SIU en vivo.
- El C210T del showroom se usa como banco de pruebas de pinpad, impresora y placa SIU. Para probar hay que detener el servicio FeedbillAgent, que toma esos dispositivos.

## Versiones
| Componente | Versión | Dónde está |
|---|---|---|
| Código | 0.7.4 | github.com/feedbill/bdm-supervisor (rama main) |
| Instalador | 0.7.4 | Depositario/BdmSupervisor/BdmSupervisor-Setup-0.7.4.exe y software.feedbill.com |
| Tester de SNBC traducido al inglés | — | Depositario/BdmSupervisor/DeviceTestorBySP_EN.exe |

## Pendientes
1. Lector de código de barras del BDM-500: el SP BCR figura fuera de línea (falla de dispositivo). Revisar driver Superlead y puerto.
2. Placa SIU del BDM-500 (firmware FV3.01H): falta el SIU_PHYSICS.ini propio de SNBC para mapear luces y sensores. Con el ini del BDM-100 el sensor de bolsa aparece como "sísmico" (IN1).
3. Consulta abierta a SNBC: el SP informa fwSafeDoor=1 (puerta abierta en el estándar CIM) y lo rotula "no soportado".
4. C210T: confirmar que las luces guía titilan físicamente y qué significa el valor 1 en puertas y sensores.
5. Pinpad: probar el cambio de la 0.7.4 (GET_DATA 407) con claves cargadas.

## Datos que conviene tener a mano
- Los servicios XFS del SP de SNBC están en HKEY_USERS\.DEFAULT\XFS\LOGICAL_SERVICES, no en HKLM.
- El SP corre en la sesión interactiva: desde SSH sin escritorio, WFSOpen devuelve CONNECTION_LOST (-54).
- La app como VDA se registra con TestTool = <ruta>\BdmSupervisor.exe --vda, sin comillas.
- C210T: placa SIU en COM1; impresora BT-NH80M en COM5 a 38400.

## Dónde está cada cosa
- Código y notas técnicas: esta carpeta (Depositario/BdmSupervisor/src en la Mac).
- Capturas, documentos y etiquetas de conexiones: Depositario/BdmSupervisor/capturas_0.7.0 y docs.
- Estado general de la implementación en Galicia: Depositario/ESTADO_BDM-500.md.
