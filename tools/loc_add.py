import re,sys
p=sys.argv[1]
s=open(p,encoding='utf-8-sig').read()
whole={
"Validador":"Validator","Impresora de tickets":"Receipt printer","Lector de tarjeta magnética":"Magnetic card reader","Lector NFC":"NFC reader",
"Teclado pinpad":"PIN pad","Lector de código de barras":"Barcode reader","Cámaras":"Cameras","Lector de huella":"Fingerprint reader",
"Sensores e indicadores":"Sensors and indicators","IMPRESORA DE TICKETS":"RECEIPT PRINTER","LECTOR DE TARJETA MAGNÉTICA":"MAGNETIC CARD READER",
"LECTOR NFC":"NFC READER","TECLADO PINPAD":"PIN PAD","LECTOR DE CÓDIGO DE BARRAS":"BARCODE READER","CÁMARAS":"CAMERAS","LECTOR DE HUELLA":"FINGERPRINT READER",
"SENSORES E INDICADORES":"SENSORS AND INDICATORS","Parámetros SP":"SP parameters","Prueba":"Test","ACTUALIZAR ESTADO":"REFRESH STATUS",
"IMPRIMIR TICKET DE PRUEBA":"PRINT TEST RECEIPT","LEER TARJETA":"READ CARD","LEER TARJETA SIN CONTACTO":"READ CONTACTLESS CARD","PROBAR TECLADO":"TEST KEYPAD",
"PROBAR INDICADORES LUMINOSOS":"TEST GUIDE LIGHTS","TOMAR FOTO":"TAKE PICTURE","LEER CÓDIGO":"READ CODE","Leyendo estado...":"Reading status...",
"Dispositivo":"Device","Papel en la salida":"Receipt at the exit","Sin papel en la salida":"No receipt at the exit","ATASCADO":"JAMMED","No soportado":"Not supported",
"Desconocido":"Unknown","Entrando":"Entering","Retraído":"Retracted","Lleno":"Full","Bajo":"Low","SIN PAPEL":"PAPER OUT","VACÍO":"EMPTY","Tarjeta adentro":"Card inside",
"Sin tarjeta":"No card","ATASCADA":"JAMMED","Retenida en la boca":"Latched","Lista":"Ready","NO LISTA":"NOT READY","SIN INICIALIZAR":"NOT INITIALIZED","Ocupada":"Busy",
"Indefinida":"Undefined","Inicializada":"Initialized","Encendido":"On","Apagado":"Off","INOPERATIVO":"INOPERATIVE","Cerrada":"Closed","ABIERTA":"OPEN",
"Cerrada con llave":"Locked","Cerrada con cerrojo":"Bolted","FORZADA":"FORCED","Inactivo":"Inactive","Activo":"Active","Apagada":"Off","Titilando lento":"Slow flash",
"Titilando":"Flashing","Titilando rápido":"Quick flash","Encendida":"On","Papel":"Paper","Ticket en la salida":"Receipt at the exit","Tóner":"Toner","Tinta":"Ink",
"Tarjeta":"Card","Tarjetas retenidas":"Retained cards","Encriptación":"Encryption","Escáner":"Scanner","Servicio XFS":"XFS service",
"Puerta del gabinete":"Cabinet door","Puerta de la caja fuerte":"Safe door","Protector antivandálico":"Vandal shield","Llave de operador":"Operator switch",
"Antisabotaje":"Tamper","Antisabotaje interno":"Internal tamper","Sísmico":"Seismic","Calor":"Heat","Proximidad":"Proximity","Luz ambiente":"Ambient light",
"Botón de arranque":"Boot switch","Pantalla del cliente":"Customer display","Botón de llamada":"Call button","Auricular":"Handset","Entrada general":"General input",
"Indicador abierto/cerrado":"Open/closed indicator","Luz de frente":"Fascia light","Calefacción":"Heating","Volumen de auriculares":"Headset volume",
"Estado vacío":"Empty status","Probando…":"Testing…","DESBLOQUEAR EDICIÓN":"UNLOCK EDITING","BLOQUEAR EDICIÓN":"LOCK EDITING","GUARDAR CAMBIOS":"SAVE CHANGES",
"VOLVER A LEER":"RELOAD","EXPORTAR REPORTE":"EXPORT REPORT","Grupo":"Group","Parámetro":"Parameter","Valor":"Value","Archivo / clave":"File / key","Descripción":"Description",
"Parámetros del SP":"SP parameters","ACEPTAR":"OK","CANCELAR":"CANCEL","Contraseña incorrecta.":"Wrong password.",
"Velocidad":"Speed","Depósito":"Deposit","Placa SIU":"SIU board","Supervisor (VDM)":"Supervisor (VDM)","Moneda":"Currency",
}
phr={
"EN LÍNEA · SIN ALERTAS":"ONLINE · NO ALERTS","SIN DISPOSITIVO / NO RESPONDE · ":"NO DEVICE / NOT RESPONDING · "," · VERIFICAR":" · CHECK",
"FUERA DE LÍNEA":"OFFLINE","EN LÍNEA":"ONLINE","SIN DISPOSITIVO":"NO DEVICE","ERROR DE HARDWARE":"HARDWARE ERROR","ERROR DE USUARIO":"USER ERROR","OCUPADO":"BUSY","APAGADO":"POWERED OFF",
"No se pudo abrir (":"Could not open (","Error al leer el estado: ":"Error reading status: ","Información adicional del SP: ":"Additional SP information: ",
"Servicio lógico XFS: ":"XFS logical service: ","Luz guía · ":"Guide light · ","Lectora de tarjeta":"Card reader","Dispensador de sobres":"Envelope dispenser",
"Dispensador":"Dispenser","Monedas":"Coins","Impresora de recibos":"Receipt printer","Impresora de libretas":"Passbook printer","Depósito de sobres":"Envelope depository",
"Aceptador de billetes":"Bill acceptor","Impresora de documentos":"Document printer","Aceptador de monedas":"Coin acceptor",
"Cámara de ambiente":"Room camera","Cámara de cliente":"Customer camera","Cámara de salida":"Exit slot camera"," · fotos: ":" · pictures: "," · almacenamiento: ":" · storage: ",
"INOPERATIVA":"INOPERATIVE","No soportada":"Not supported","Casi llena":"Almost full","LLENA":"FULL",
"Imprime un ticket corto con fecha, hora y nombre del equipo, y lo corta.":"Prints a short receipt with date, time and device name, and cuts it.",
"Pulse el botón y pase o inserte una tarjeta de banda magnética (30 s). Los datos se muestran enmascarados.":"Press the button and swipe or insert a magnetic stripe card (30 s). Data is shown masked.",
"Pulse el botón y acerque una tarjeta sin contacto al lector (30 s). Se muestra el ATR / UID.":"Press the button and hold a contactless card near the reader (30 s). The ATR / UID is shown.",
"Pulse el botón y presione teclas en el pinpad; termina con ENTER o CANCEL (30 s).":"Press the button and press keys on the PIN pad; it ends with ENTER or CANCEL (30 s).",
"Hace titilar 5 segundos todas las luces guía disponibles y después las apaga.":"Flashes all available guide lights for 5 seconds and then turns them off.",
"Toma una foto con cada cámara disponible y muestra la última imagen generada.":"Takes a picture with each available camera and shows the latest image.",
"Pulse el botón y acerque un código de barras o QR al lector (20 s).":"Press the button and hold a barcode or QR code near the reader (20 s).",
"Este módulo solo informa su estado.":"This module only reports its status.",
"Pase o inserte la tarjeta…":"Swipe or insert the card…","Acerque la tarjeta al lector…":"Hold the card near the reader…",
"Presione teclas en el pinpad y termine con ENTER…":"Press keys on the PIN pad and finish with ENTER…","Acerque el código al lector…":"Hold the code near the reader…",
"Ticket de prueba enviado (":"Test receipt sent (","corte OK":"cut OK","corte: ":"cut: ","No se pudo imprimir: ":"Could not print: ",
"Pista ":"Track ",": faltante":": missing",": inválida":": invalid",": muy larga":": too long",": muy corta":": too short",": no soportada":": not supported",
"El lector respondió sin datos.":"The reader returned no data.","No se presentó ninguna tarjeta (tiempo agotado).":"No card was presented (timeout).",
"Error de lectura: ":"Read error: ","El pinpad terminó sin teclas.":"The PIN pad finished without keys.","Teclas recibidas: ":"Keys received: ",
"No se presionó ninguna tecla (tiempo agotado).":"No key was pressed (timeout).","Error del pinpad: ":"PIN pad error: ",
"La placa SIU no informa luces guía disponibles.":"The SIU board reports no available guide lights.","Titilaron y se apagaron: ":"Flashed and turned off: ",
"Sin respuesta: ":"No response: ",": foto tomada":": picture taken","No se encontró el archivo de la imagen (configure Paths.CameraImageDir).":"Image file not found (set Paths.CameraImageDir).",
"Imagen: ":"Image: ","No se leyó ningún código (tiempo agotado).":"No code was read (timeout).","Error del lector: ":"Reader error: ",
"No se pudo abrir el servicio: ":"Could not open the service: ","Este módulo no tiene prueba.":"This module has no test.","simbología ":"symbology ",
"Parámetros del Service Provider XFS":"XFS Service Provider parameters","No se encontró el SP en ":"SP not found in ","Error al leer los parámetros: ":"Error reading the parameters: ",
"Hay cambios sin guardar. ¿Descartarlos y volver a leer?":"There are unsaved changes. Discard them and reload?",
"Contraseña para modificar los parámetros del SP":"Password to modify the SP parameters",
"Edición habilitada: doble clic en la columna Valor para cambiarlo.":"Editing enabled: double-click the Value column to change it.",
"Solo lectura. Para modificar valores desbloquee la edición con la contraseña.":"Read only. Unlock editing with the password to change values.",
"la velocidad debe ser un número entre 300 y 1200":"the speed must be a number between 300 and 1200","los reintentos deben ser un número entre 0 y 10":"retries must be a number between 0 and 10",
"la moneda debe ser un código ISO de 3 letras (ARS, USD...)":"the currency must be a 3-letter ISO code (ARS, USD...)","debe ser un número entero":"must be an integer",
"debe ser 0 o 1":"must be 0 or 1","no puede quedar vacío":"cannot be empty",
"Se van a guardar estos cambios en la configuración del Service Provider:":"These changes will be saved to the Service Provider configuration:",
"¿Continuar?":"Continue?","Cambios guardados:":"Changes saved:","Los cambios los toma el Service Provider al reiniciar el equipo.":"The Service Provider applies the changes when the device is restarted.",
"Antes de guardar se hace una copia de cada archivo (.bak-fecha).":"Each file is backed up (.bak-date) before saving.",
"Error al guardar: ":"Error saving: ","Error al exportar: ":"Error exporting: ","Reporte de parámetros del Service Provider XFS":"XFS Service Provider parameter report",
"Velocidad de depósito (clasificación)":"Deposit speed (sorting)","Velocidad de conteo simple":"Simple count speed","Velocidad de conteo por denominación":"Count-by-denomination speed",
"Billetes por minuto al depositar con validación. 300–1200 (Galicia: 850)":"Notes per minute when depositing with validation. 300–1200 (Galicia: 850)",
"Billetes por minuto en conteo sin clasificar. 300–1200 (Galicia: 850)":"Notes per minute in unsorted counting. 300–1200 (Galicia: 850)",
"Billetes por minuto en conteo por denominación. 300–1200":"Notes per minute in count by denomination. 300–1200",
"Reintentos luego de depósito no exitoso":"Retries after unsuccessful deposit","Reintentos luego de depósito exitoso":"Retries after successful deposit",
"Aleteos: veces que reintenta prensar los billetes si el prensado falla (SNBC: retry)":"Number of retries for failed banknote pressing (SNBC: retry)",
"Aleteos adicionales después de un prensado exitoso (SNBC: successretry)":"Number of retries after successful pressing (SNBC: successretry)",
"Billete en boca: seguir prensando":"Note at the exit: keep pressing","1 = si detecta billete en la boca sigue prensando; 0 = corta con error":"1 = keeps pressing if a note is detected at the exit; 0 = stops with an error",
"Máximo de billetes por depósito":"Maximum notes per deposit","Límite por operación de depósito (CIM MaxCashInItems)":"Limit per deposit operation (CIM MaxCashInItems)",
"Capacidad del escrow":"Escrow capacity","Billetes en el apilador temporal":"Notes in the escrow","Código ISO de la moneda del validador (ARS, USD...)":"ISO currency code of the validator (ARS, USD...)",
"Compuerta validador → bolsa (drop door)":"Validator → bag gate (drop door)","1 = equipo con compuerta entre validador y bolsa; 0 = sin compuerta (Galicia)":"1 = device with a gate between validator and bag; 0 = no gate (Galicia)",
"Shutter de entrada al inicializar":"Entry shutter on initialize","Shutter de salida al inicializar":"Exit shutter on initialize","Shutter de entrada":"Entry shutter","Shutter de salida":"Exit shutter","Shutter del escrow":"Escrow shutter",
"1 = soporta shutter en la boca de depósito":"1 = shutter supported at the deposit slot","1 = soporta shutter en la salida":"1 = shutter supported at the exit","1 = soporta shutter en el apilador temporal":"1 = shutter supported at the escrow",
"Puerta de bóveda monitoreada":"Safe door monitored","1 = el SP monitorea la puerta de bóveda; 0 = no":"1 = the SP monitors the safe door; 0 = no",
"SP controla el shutter":"SP controls the shutter","1 = el SP maneja los shutters; 0 = no":"1 = the SP drives the shutters; 0 = no",
"1 = habilita el shutter de entrada al inicializar":"1 = enables the entry shutter on initialize","1 = habilita el shutter de salida al inicializar":"1 = enables the exit shutter on initialize",
"Abrir shutter después de reset":"Open shutter after reset","1 = abre el shutter al terminar un reset":"1 = opens the shutter after a reset",
"Shutter eléctrico (placa SIU)":"Electric shutter (SIU board)","1 = shutter eléctrico controlado por placa SIU (fábrica); 0 = sin shutter eléctrico / mecánico (Galicia)":"1 = electric shutter driven by the SIU board (factory); 0 = no electric shutter / mechanical (Galicia)",
"Puerto de la placa SIU":"SIU board port","Puerto serie de la placa SIU":"SIU board serial port","Cantidad de placas":"Number of boards","SNBC indica 1 para configuración sin shutter eléctrico":"SNBC specifies 1 for the configuration without electric shutter",
"Aplicación de supervisor (TestTool)":"Supervisor application (TestTool)","Ejecutable que el SP lanza al entrar en modo supervisor":"Executable the SP launches when entering supervisor mode",
"Espera de ACK al entrar (ms)":"ACK wait on entry (ms)","0 = sin espera":"0 = no wait",
}
# Phrases block
m=re.search(r'(private static readonly \(string Es, string En\)\[\] Phrases =\s*\[\n)(.*?)(\n\s*\];)',s,re.S)
body=m.group(2)
ent=re.findall(r'^\s*\("((?:[^"\\]|\\.)*)",\s*"((?:[^"\\]|\\.)*)"\),?\s*$',body,re.M)
cnt=len([l for l in body.split('\n') if l.strip()])
assert len(ent)==cnt, (len(ent),cnt)
d=dict(ent)
for k,v in phr.items(): d[k]=v
items=sorted(d.items(),key=lambda kv:-len(kv[0]))
nb='\n'.join(f'        ("{k}", "{v}"),' for k,v in items)
s=s[:m.start(2)]+nb+s[m.end(2):]
# Whole
m=re.search(r'(Whole = new\(\)\s*\{\n)(.*?)(\n\s*\};)',s,re.S)
existing=set(re.findall(r'\["((?:[^"\\]|\\.)*)"\]',m.group(2)))
add='\n'.join(f'        ["{k}"] = "{v}",' for k,v in whole.items() if k not in existing)
s=s[:m.end(2)]+'\n'+add+s[m.end(2):]
open(p,'w',encoding='utf-8-sig').write(s)
print(len(items),"phrases; whole added",len([k for k in whole if k not in existing]))
