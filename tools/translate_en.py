#!/usr/bin/env python3
"""Genera la versión en inglés de BDM Supervisor a partir del código en español.

Uso: python3 translate_en.py <src_es> <src_en>
Copia el proyecto y traduce solo los textos visibles: literales de cadena en .cs y atributos
Text/Content/Header/Title/ToolTip en .xaml. Se vuelve a correr en cada versión nueva.
"""
import os, re, shutil, sys

# Frases (se reemplazan dentro de los literales, la más larga primero)
PHRASES = {
    # ---- Sensores y diagrama
    "Sensor de alimentación": "Feed sensor",
    "Billete en posición de alimentación": "Note in feed position",
    "Sensores CIS derecho e izquierdo": "CIS right and left sensors",
    "Contador de la boca de rechazo": "Reject pocket counter",
    "Detección de la boca de rechazo": "Reject pocket detection",
    "Boca de rechazo llena": "Reject pocket full",
    "Sensor de espesor": "Thickness sensor",
    "Contador del escrow": "Escrow counter",
    "Detección del escrow": "Escrow detection",
    "Curva derecho / izquierdo y HD trasero": "Turn right / left and rear HD",
    "Sensores antipesca y de caída": "Anti-fishing and drop sensors",
    "Compuerta de escrow abierta": "Escrow gate open",
    "Compuerta de escrow cerrada": "Escrow gate closed",
    "Traba de la compuerta abierta": "Gate lock open",
    "Traba de la compuerta cerrada": "Gate lock closed",
    "Compuerta de caída abierta": "Drop door open",
    "Compuerta de caída cerrada": "Drop door closed",
    "Motor principal bloqueado": "Main motor blocked",
    "Motor de alimentación bloqueado": "Feed motor blocked",
    "Atasco de billete": "Note jam",
    "Atasco en la alimentación": "Jam in the feed",
    "Atasco en captura de datos (CIS / magnético)": "Jam in data capture (CIS / magnetic)",
    "Atasco en la curva del transporte": "Jam in the transport turn",
    "Atasco en el canal de salida al escrow": "Jam in the output channel to escrow",
    "Error del solenoide de desvío": "Diverter solenoid error",
    "Billetes trabados en el escrow": "Notes stuck in the escrow",
    "Billetes trabados en la boca de rechazo": "Notes stuck in the reject pocket",
    "Tapa superior levantada": "Top cover lifted",
    "Error del módulo CIS": "CIS module error",
    "Error del módulo magnético": "Magnetic module error",
    "Error del módulo de espesor": "Thickness module error",
    "Error de la compuerta de caída": "Drop door error",
    "Error de la compuerta de escrow": "Escrow gate error",
    "Error de la traba de la compuerta de escrow": "Escrow gate lock error",
    "Pendiente de inicializar": "Pending initialization",
    "Depósito en escrow completo": "Escrow deposit complete",
    "Depósito en curso": "Deposit in progress",
    "Encendido": "Powered on",
    "Tapa frontal abierta: verificar que no queden billetes atascados y cerrar la tapa": "Front cover open: check that no notes are jammed and close the cover",
    "Tapa trasera abierta: verificar que no queden billetes atascados y cerrar la tapa": "Rear cover open: check that no notes are jammed and close the cover",
    "Tapa superior (aviso del SDK): ABIERTA — verificar atascos y cerrarla": "Top cover (SDK flag): OPEN — check for jams and close it",
    "Tapa frontal": "Front cover",
    "Tapa trasera": "Rear cover",
    "Hopper (entrada)": "Hopper (input)",
    "Compuerta escrow": "Escrow gate",
    "Compuerta de caída": "Drop door",
    "Sin lectura en vivo": "No live reading",
    "VERIFICAR · ": "CHECK · ",
    "atasco en la alimentación": "jam in the feed",
    "hay billetes en la boca": "notes in the hopper",
    "billetes trabados": "notes stuck",
    "hay billetes en el escrow": "notes in the escrow",
    "boca de rechazo llena": "reject pocket full",
    "hay billetes en el rechazo": "notes in the reject pocket",
    "atasco en el transporte": "jam in the transport",
    "hay billetes en el transporte": "notes in the transport",
    "falla de la compuerta": "gate fault",
    "sensor antipesca tapado": "anti-fishing sensor covered",
    "Pulse «Leer sensores en vivo»": "Press «Read live sensors»",
    "VALIDADOR: SIN LECTURA": "VALIDATOR: NO READING",
    "VALIDADOR: VERIFICAR": "VALIDATOR: CHECK",
    "VALIDADOR: ": "VALIDATOR: ",
    "Sin conexión con el equipo": "No connection to the device",
    "(para ver sensores en vivo: «Leer sensores en vivo»)": "(live sensors: «Read live sensors»)",
    "Por XFS: ": "Via XFS: ",
    "Último error informado por XFS: ": "Last error reported by XFS: ",
    "Sin errores informados por XFS": "No errors reported by XFS",
    "Último error: ": "Last error: ",
    "Lectura en vivo pausada": "Live reading paused",
    "Sin atascos ni errores": "No jams or errors",
    "el validador informa estado de error": "the validator reports an error state",
    "anomalía en una compuerta": "gate anomaly",
    "Traba de la compuerta de escrow: ": "Escrow gate lock: ",
    "sin posición": "no position",
    "Bolsa en posición: ": "Bag in position: ",
    "Puerta de la caja fuerte (sensor): ": "Safe door (sensor): ",
    " · traba de la puerta: ": " · door lock: ",
    "Bolsa casi llena: ": "Bag almost full: ",
    " · bolsa llena: ": " · bag full: ",
    " · vibración: ": " · vibration: ",
    "billetes en la boca de entrada": "notes in the input pocket",
    "billetes en el transporte": "notes in the transport",
    "billetes en el escrow": "notes in the escrow",
    "billetes en la boca de rechazo": "notes in the reject pocket",
    "compuerta de escrow con anomalía": "escrow gate anomaly",
    "escrow abierto": "escrow open",
    "escrow cerrado": "escrow closed",
    "compuerta de caída con anomalía": "drop door anomaly",
    "compuerta de caída abierta": "drop door open",
    "compuerta de caída cerrada": "drop door closed",
    "Lectura: ": "Reading: ",
    "CERRAR COMPUERTA DE CAÍDA": "CLOSE DROP DOOR",
    "ABRIR COMPUERTA DE CAÍDA": "OPEN DROP DOOR",
    "CERRAR ESCROW": "CLOSE ESCROW",
    "ABRIR ESCROW": "OPEN ESCROW",
    "Iniciando la lectura de sensores en vivo...": "Starting live sensor reading...",
    "Lectura de sensores: ": "Sensor reading: ",
    "Error leyendo sensores: ": "Error reading sensors: ",
    "Iniciando lectura en vivo...": "Starting live reading...",
    # ---- Ventana principal
    "Conectando al equipo...": "Connecting to the device...",
    "VDM disponible: Softy liberará el equipo automáticamente al entrar.": "VDM available: the banking application releases the device automatically on entry.",
    "El Service Provider no expone VDM. Antes de entrar, detenga la aplicación bancaria (Softy) manualmente.": "The Service Provider does not expose VDM. Stop the banking application manually before entering.",
    "Modo supervisor activado desde la aplicación bancaria": "Supervisor mode activated from the banking application",
    "Leyendo estado...": "Reading status...",
    "MANTENIMIENTO DIRECTO": "DIRECT MAINTENANCE",
    "Validador (BNE)": "Validator (BNE)",
    "Aceptador / unidades": "Acceptor / units",
    "Puerta de bóveda": "Safe door",
    "No monitoreada": "Not monitored",
    "Compuerta de escrow": "Escrow gate",
    "Transporte ": "Transport ",
    "Último error del validador": "Last validator error",
    "Sin errores": "No errors",
    "Entrando en modo supervisor (la aplicación bancaria libera el equipo)...": "Entering supervisor mode (the banking application releases the device)...",
    "Volviendo a operación normal (el Service Provider tarda hasta un minuto)...": "Returning to normal operation (the Service Provider takes up to one minute)...",
    "Saliendo del modo supervisor (el equipo vuelve a la aplicación bancaria)...": "Leaving supervisor mode (the device returns to the banking application)...",
    "Cerrando: volviendo a operación normal...": "Closing: returning to normal operation...",
    "Cerrando: saliendo del modo supervisor...": "Closing: leaving supervisor mode...",
    "SUPERVISOR ACTIVO": "SUPERVISOR ACTIVE",
    "SUPERVISOR INACTIVO": "SUPERVISOR INACTIVE",
    "SALIR DEL MODO SUPERVISOR": "EXIT SUPERVISOR MODE",
    "ENTRAR EN MODO SUPERVISOR": "ENTER SUPERVISOR MODE",
    "Reset en curso (puede tardar)...": "Reset in progress (may take a while)...",
    "Pausando la lectura en vivo: el Service Provider retoma el validador (hasta un minuto)...": "Pausing live reading: the Service Provider takes back the validator (up to one minute)...",
    "El validador informa una anomalía en ": "The validator reports an anomaly in ",
    "la compuerta de escrow": "the escrow gate",
    "la compuerta de caída": "the drop door",
    "Abriendo la compuerta de escrow...": "Opening the escrow gate...",
    "Cerrando la compuerta de escrow...": "Closing the escrow gate...",
    "Abriendo la compuerta de caída...": "Opening the drop door...",
    "Cerrando la compuerta de caída...": "Closing the drop door...",
    "ciclo(s) de apertura y cierre de la compuerta de caída": "drop door open/close cycle(s)",
    "ciclo(s) de la compuerta de caída": "drop door cycle(s)",
    "Aleteo terminado: ": "Flutter finished: ",
    "Aleteo: ": "Flutter: ",
    "Coloque los billetes en la boca de depósito…": "Place the notes in the deposit hopper…",
    "Cancelando…": "Cancelling…",
    "Prueba de depósito: esperando billetes…": "Deposit test: waiting for notes…",
    "Prueba de depósito: ": "Deposit test: ",
    "Prueba de depósito": "Deposit test",
    "Billetes contados: ": "Notes counted: ",
    "Rechazados: ": "Rejected: ",
    "(quedan en la salida de rechazo)": "(left in the reject pocket)",
    "Sí = depositar (pasan a la bolsa)": "Yes = deposit (notes go to the bag)",
    "No = devolver los billetes": "No = return the notes",
    "Depositando (los billetes pasan a la bolsa)…": "Depositing (notes go to the bag)…",
    "Devolviendo los billetes…": "Returning the notes…",
    "Esto pone en cero los contadores de la bolsa. Hágalo solo después de retirar la bolsa llena y colocar la nueva. ¿Continuar?": "This resets the bag counters to zero. Do it only after removing the full bag and fitting a new one. Continue?",
    "Cambio de bolsa": "Bag exchange",
    "Registrando cambio de bolsa...": "Registering bag exchange...",
    "Leyendo capabilities...": "Reading capabilities...",
    "Capabilities del validador": "Validator capabilities",
    "(sin estado)": "(no status)",
    "Exportando bundle...": "Exporting bundle...",
    "Entre en modo supervisor antes de abrir la herramienta del validador.": "Enter supervisor mode before opening the validator tool.",
    "No se encontró la herramienta SNBC. Configure Paths.SnbcToolPath en appsettings.json.": "SNBC tool not found. Set Paths.SnbcToolPath in appsettings.json.",
    "Abriendo tool SNBC: ": "Opening SNBC tool: ",
    "El SP de SNBC lanza otra aplicación al entrar en modo supervisor (TestTool = ": "The SNBC SP launches another application in supervisor mode (TestTool = ",
    "Si SNBC actualizó el SP, el valor se perdió; con Reparar vuelve a apuntar a esta aplicación.": "If the SP was updated the value was lost; Repair points it back to this application.",
    "TestTool del SP no apunta a esta app: ": "SP TestTool does not point to this app: ",
    "TestTool actualizado. El SP lo toma en la próxima entrada al modo supervisor.": "TestTool updated. The SP uses it the next time supervisor mode is entered.",
    "Operación cancelada por el usuario": "Operation cancelled by the user",
    "El validador informa: ": "The validator reports: ",
    "Validador: ": "Validator: ",
    "ATASCO. ": "JAM. ",
    "ATASCO ": "JAM ",
    "atasco · ": "jam · ",
    # ---- Servicios
    "Equipo: ": "Machine: ",
    "Usuario: ": "User: ",
    "SO: ": "OS: ",
    "Fecha: ": "Date: ",
    "Bundle de diagnóstico exportado: ": "Diagnostic bundle exported: ",
    "SIMULADOR BDM-500 (sin hardware)": "BDM-500 SIMULATOR (no hardware)",
    "Mock: conectado": "Mock: connected",
    "Mock: cambio de bolsa, contadores en 0": "Mock: bag exchange, counters reset",
    "Esta operación requiere el modo supervisor activo.": "This operation requires supervisor mode.",
    "No se encontró el SDK del validador": "Validator SDK not found",
    "El SDK no pudo abrir el validador. Verifique que el Service Provider esté detenido y el USB conectado.": "The SDK could not open the validator. Check that the Service Provider is stopped and the USB cable is connected.",
    "Mantenimiento directo: validador abierto con el SDK de SNBC": "Direct maintenance: validator opened with the SNBC SDK",
    "Mantenimiento directo: validador cerrado": "Direct maintenance: validator closed",
    "Mantenimiento directo: se detiene el Service Provider del validador para mover las compuertas con el SDK de SNBC.": "Direct maintenance: the validator Service Provider is stopped to use the SNBC SDK.",
    "el validador devolvió el error": "the validator returned error",
    "Leer estado": "Read status",
    "Abrir compuerta de escrow": "Open escrow gate",
    "Cerrar compuerta de escrow": "Close escrow gate",
    "Abrir compuerta de caída": "Open drop door",
    "Cerrar compuerta de caída": "Close drop door",
    "Leer sensores": "Read sensors",
    "No conectado al SP XFS": "Not connected to the XFS SP",
    "No conectado": "Not connected",
    "Servicio VDM no disponible": "VDM service not available",
    "Se usará modo exclusivo: la app bancaria debe estar detenida.": "Exclusive mode will be used: the banking app must be stopped.",
    "El Service Provider todavía está iniciando": "The Service Provider is still starting",
    "Reintento ": "Retry ",
    " de 5...": " of 5...",
    "VDM disponible. Estado: ": "VDM available. Status: ",
    "Volviendo a operación normal: el Service Provider retoma el validador (tarda hasta un minuto)...": "Returning to normal operation: the Service Provider takes back the validator (up to one minute)...",
    "Billetes en el transporte (posible atasco/jam)": "Notes in the transport (possible jam)",
    "Entrando en modo supervisor sin VDM: verifique que Softy esté detenida.": "Entering supervisor mode without VDM: make sure the banking application is stopped.",
    "VDM estado: ": "VDM status: ",
    "Lanzados por el SP; VDM estado: ": "Launched by the SP; VDM status: ",
    " billetes contados, ": " notes counted, ",
    " rechazados": " rejected",
    "No se colocaron billetes en la boca de depósito. La prueba se canceló.": "No notes were placed in the deposit hopper. The test was cancelled.",
    "Coloque los billetes en la boca de depósito… (": "Place the notes in the deposit hopper… (",
    "El equipo está en mantenimiento directo (compuertas). Pulse «Volver a operación normal» para usar esta función.": "The device is in direct maintenance. Return to normal operation to use this function.",
    "Ya había un depósito activo en el SP: se continúa sobre ese.": "A deposit was already active in the SP: continuing with it.",
    "VDM: registrado a eventos": "VDM: registered for events",
    "VDM sin ventana de eventos (Attach no llamado)": "VDM without event window (Attach not called)",
    "Ya hay un pedido VDM en curso": "A VDM request is already in progress",
    " proactivo (req ": " proactive (req ",
    "VDM: sin respuesta del SP a ": "VDM: no SP response to ",
    "VDM evento de sistema ": "VDM system event ",
    "VDM evento ": "VDM event ",
    " completado por el SP": " completed by the SP",
    "evento ": "event ",
    "XFS Manager iniciado: ": "XFS Manager started: ",
    "Servicio '": "Service '",
    "' abierto (hService=": "' opened (hService=",
    "' cerrado": "' closed",
    "No existe la clave ": "Registry key not found: ",
    "TestTool reparado: ": "TestTool repaired: ",
    "Parámetro SP cambiado: ": "SP parameter changed: ",
    " (antes ": " (was ",
    # ---- XAML
    "Depositario de billetes": "Banknote depositor",
    "Módulos": "Modules",
    "ACTUALIZAR ESTADO": "REFRESH STATUS",
    "Unidades de efectivo": "Cash units",
    "Extra (firmware / template)": "Extra (firmware / template)",
    "Última lectura": "Last reading",
    "PRUEBA DE DEPÓSITO": "DEPOSIT TEST",
    "RESET DEL EQUIPO": "RESET DEVICE",
    "CAMBIO DE BOLSA": "BAG EXCHANGE",
    "PROBAR ALETEO": "TEST FLUTTER",
    "Atasco / error": "Jam / error",
    "Tapa abierta": "Cover open",
    "Estado del validador": "Validator status",
    "Atascos y errores": "Jams and errors",
    "Caja fuerte y bolsa": "Safe and bag",
    "Sensores tapados": "Covered sensors",
    "Herramientas": "Tools",
    "EXPORTAR BUNDLE DE DIAGNÓSTICO (ZIP)": "EXPORT DIAGNOSTIC BUNDLE (ZIP)",
    "Junta los logs de esta app, los del Service Provider SNBC y datos del equipo para enviarlos a soporte Feedbill.": "Collects the logs of this app and of the SNBC Service Provider, plus device data, to send to support.",
    "ABRIR CARPETA DE LOGS": "OPEN LOG FOLDER",
    "LEER CAPABILITIES DEL EQUIPO": "READ DEVICE CAPABILITIES",
    "REPARAR": "REPAIR",
}
# Literales completos (solo si el literal entero es exactamente esto)
WHOLE = {
    "Listo": "Ready", "Error": "Error", "Estado": "Status", "Libre": "Free", "Tapado": "Covered",
    "Cerrada": "Closed", "Abierta": "Open", "Escrow": "Escrow", "Rechazo": "Reject", "Transporte": "Transport",
    "Antipesca": "Anti-fishing", "Sin lectura": "No reading", "Mantenimiento": "Maintenance", "Tipo": "Type",
    "Billetes": "Notes", "Máx.": "Max.", "estado ": "status ", "Error: ": "Error: ", "CANCELAR": "CANCEL",
    "Cancelar": "Cancel", "Equipo": "Device",
}
ORDER = sorted(PHRASES, key=len, reverse=True)

def tr(text):
    if text in WHOLE:
        return WHOLE[text]
    for k in ORDER:
        if k in text:
            text = text.replace(k, PHRASES[k])
    return text

def tr_cs(src):
    # Solo literales de cadena (no comentarios ni identificadores)
    out, i, n = [], 0, len(src)
    while i < n:
        c = src[i]
        if src.startswith('//', i):
            j = src.find('\n', i); j = n if j < 0 else j
            out.append(src[i:j]); i = j; continue
        if src.startswith('/*', i):
            j = src.find('*/', i) + 2
            out.append(src[i:j]); i = j; continue
        if c == "'":
            j = i + 1
            while src[j] != "'": j += 2 if src[j] == '\\' else 1
            out.append(src[i:j + 1]); i = j + 1; continue
        if c == '"':
            verbatim = src[max(0, i - 2):i].endswith('@') or src[max(0, i - 2):i] in ('@$', '$@')
            j = i + 1
            if verbatim:
                while True:
                    if src[j] == '"' and j + 1 < n and src[j + 1] == '"': j += 2; continue
                    if src[j] == '"': break
                    j += 1
            else:
                while src[j] != '"' and src[j] != '\n': j += 2 if src[j] == '\\' else 1
            out.append('"' + tr(src[i + 1:j]) + src[j]); i = j + 1; continue
        out.append(c); i += 1
    return ''.join(out)

def tr_xaml(src):
    return re.sub(r'((?:Text|Content|Header|Title|ToolTip)=")([^"{]*)(")', lambda m: m.group(1) + tr(m.group(2)) + m.group(3), src)

ISS_LINES = {
    'Name: "spanish"': 'Name: "english"; MessagesFile: "compiler:Default.isl"',
    'Name: "desktopicon"': 'Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"',
    'Name: "vdm"': 'Name: "vdm"; Description: "Register this application as the supervisor tool (VDM / TestTool) of the SNBC Service Provider"; GroupDescription: "SNBC XFS SP integration:"; Check: SpInstalled',
    'Name: "profile";': 'Name: "profile"; Description: "Configure the SP for the installed hardware:"; GroupDescription: "BDM-500 profile:"; Flags: checkablealone unchecked; Check: SpInstalled',
    'Name: "profile\\galicia"': 'Name: "profile\\galicia"; Description: "Without electric shutter (shutter disabled)"; Flags: exclusive; Check: SpInstalled',
    'Name: "profile\\electrico"': 'Name: "profile\\electrico"; Description: "With electric shutter (SNBC factory configuration)"; Flags: exclusive unchecked; Check: SpInstalled',
}

def tr_iss(src):
    out = []
    for line in src.splitlines():
        for k, v in ISS_LINES.items():
            if line.startswith(k): line = v; break
        line = line.replace('Chequeo XFS (XfsCheck)', 'XFS check (XfsCheck)').replace('{group}\\Desinstalar', '{group}\\Uninstall')
        line = line.replace('Description: "Abrir {#AppName}"', 'Description: "Open {#AppName}"')
        line = re.sub(r'OutputBaseFilename=BdmSupervisor-Setup-\{#AppVersion\}', 'OutputBaseFilename=BdmSupervisor-Setup-{#AppVersion}-EN', line)
        line = line.replace('#define SrcDir "C:\\feedbill\\BdmSupervisor"', '#define SrcDir "C:\\feedbill\\BdmSupervisor_EN"')
        out.append(line)
    return '\r\n'.join(out) + '\r\n'

def main(es, en):
    if os.path.exists(en): shutil.rmtree(en)
    shutil.copytree(es, en, ignore=shutil.ignore_patterns('.git', 'bin', 'obj', 'publish', '*.exe'))
    for root, _, files in os.walk(en):
        for f in files:
            p = os.path.join(root, f)
            if f.endswith('.cs') and 'XfsCheck' not in root:
                s = open(p, encoding='utf-8-sig').read(); open(p, 'w', encoding='utf-8').write(tr_cs(s))
            elif f.endswith('.iss'):
                s = open(p, encoding='utf-8-sig').read(); open(p, 'w', encoding='utf-8-sig').write(tr_iss(s))
            elif f.endswith('.xaml'):
                s = open(p, encoding='utf-8-sig').read(); open(p, 'w', encoding='utf-8').write(tr_xaml(s))

if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
