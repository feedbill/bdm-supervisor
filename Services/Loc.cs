using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace BdmSupervisor.Services;

/// <summary>
/// Idioma de la interfaz (es / en). Los textos se escriben en español en el código y en el XAML;
/// Loc.T los traduce al inglés cuando corresponde, y Attach engancha los controles para que se
/// traduzcan solos cada vez que cambian y al cambiar de idioma. Para sumar textos nuevos, agregarlos acá.
/// </summary>
public static class Loc
{
    public static string Lang { get; private set; } = "es";
    public static event Action? Changed;
    private static readonly (string Es, string En)[] Phrases =
    [
        ("Esto pone en cero los contadores de la bolsa. Hágalo solo después de retirar la bolsa llena y colocar la nueva. ¿Continuar?", "This resets the bag counters to zero. Do it only after removing the full bag and fitting a new one. Continue?"),
        ("Mantenimiento directo: se detiene el Service Provider del validador para mover las compuertas con el SDK de SNBC.", "Direct maintenance: the validator Service Provider is stopped to use the SNBC SDK."),
        ("El equipo está en mantenimiento directo (compuertas). Pulse «Volver a operación normal» para usar esta función.", "The device is in direct maintenance. Return to normal operation to use this function."),
        ("Junta los logs de esta app, los del Service Provider SNBC y datos del equipo para enviarlos a soporte Feedbill.", "Collects the logs of this app and of the SNBC Service Provider, plus device data, to send to support."),
        ("El Service Provider no expone VDM. Antes de entrar, detenga la aplicación bancaria (Softy) manualmente.", "The Service Provider does not expose VDM. Stop the banking application manually before entering."),
        ("El SDK no pudo abrir el validador. Verifique que el Service Provider esté detenido y el USB conectado.", "The SDK could not open the validator. Check that the Service Provider is stopped and the USB cable is connected."),
        ("Volviendo a operación normal: el Service Provider retoma el validador (tarda hasta un minuto)...", "Returning to normal operation: the Service Provider takes back the validator (up to one minute)..."),
        ("Si SNBC actualizó el SP, el valor se perdió; con Reparar vuelve a apuntar a esta aplicación.", "If the SP was updated the value was lost; Repair points it back to this application."),
        ("Pausando la lectura en vivo: el Service Provider retoma el validador (hasta un minuto)...", "Pausing live reading: the Service Provider takes back the validator (up to one minute)..."),
        ("No se encontró la herramienta SNBC. Configure Paths.SnbcToolPath en appsettings.json.", "SNBC tool not found. Set Paths.SnbcToolPath in appsettings.json."),
        ("Tapa frontal abierta: verificar que no queden billetes atascados y cerrar la tapa", "Front cover open: check that no notes are jammed and close the cover"),
        ("Tapa trasera abierta: verificar que no queden billetes atascados y cerrar la tapa", "Rear cover open: check that no notes are jammed and close the cover"),
        ("El SP de SNBC lanza otra aplicación al entrar en modo supervisor (TestTool = ", "The SNBC SP launches another application in supervisor mode (TestTool = "),
        ("TestTool actualizado. El SP lo toma en la próxima entrada al modo supervisor.", "TestTool updated. The SP uses it the next time supervisor mode is entered."),
        ("Volviendo a operación normal (el Service Provider tarda hasta un minuto)...", "Returning to normal operation (the Service Provider takes up to one minute)..."),
        ("Saliendo del modo supervisor (el equipo vuelve a la aplicación bancaria)...", "Leaving supervisor mode (the device returns to the banking application)..."),
        ("Entrando en modo supervisor (la aplicación bancaria libera el equipo)...", "Entering supervisor mode (the banking application releases the device)..."),
        ("Entrando en modo supervisor sin VDM: verifique que Softy esté detenida.", "Entering supervisor mode without VDM: make sure the banking application is stopped."),
        ("No se colocaron billetes en la boca de depósito. La prueba se canceló.", "No notes were placed in the deposit hopper. The test was cancelled."),
        ("Tapa superior (aviso del SDK): ABIERTA — verificar atascos y cerrarla", "Top cover (SDK flag): OPEN — check for jams and close it"),
        ("Entre en modo supervisor antes de abrir la herramienta del validador.", "Enter supervisor mode before opening the validator tool."),
        ("VDM disponible: Softy liberará el equipo automáticamente al entrar.", "VDM available: the banking application releases the device automatically on entry."),
        ("Se usará modo exclusivo: la app bancaria debe estar detenida.", "Exclusive mode will be used: the banking app must be stopped."),
        ("Ya había un depósito activo en el SP: se continúa sobre ese.", "A deposit was already active in the SP: continuing with it."),
        ("Mantenimiento directo: validador abierto con el SDK de SNBC", "Direct maintenance: validator opened with the SNBC SDK"),
        ("ciclo(s) de apertura y cierre de la compuerta de caída", "drop door open/close cycle(s)"),
        ("Modo supervisor activado desde la aplicación bancaria", "Supervisor mode activated from the banking application"),
        ("(para ver sensores en vivo: «Leer sensores en vivo»)", "(live sensors: «Read live sensors»)"),
        ("Esta operación requiere el modo supervisor activo.", "This operation requires supervisor mode."),
        ("Billetes en el transporte (posible atasco/jam)", "Notes in the transport (possible jam)"),
        ("Coloque los billetes en la boca de depósito… (", "Place the notes in the deposit hopper… ("),
        ("VDM sin ventana de eventos (Attach no llamado)", "VDM without event window (Attach not called)"),
        ("Atasco en captura de datos (CIS / magnético)", "Jam in data capture (CIS / magnetic)"),
        ("Coloque los billetes en la boca de depósito…", "Place the notes in the deposit hopper…"),
        ("Depositando (los billetes pasan a la bolsa)…", "Depositing (notes go to the bag)…"),
        ("Error de la traba de la compuerta de escrow", "Escrow gate lock error"),
        ("Iniciando la lectura de sensores en vivo...", "Starting live sensor reading..."),
        ("El Service Provider todavía está iniciando", "The Service Provider is still starting"),
        ("Cerrando: volviendo a operación normal...", "Closing: returning to normal operation..."),
        ("Cerrando: saliendo del modo supervisor...", "Closing: leaving supervisor mode..."),
        ("Mantenimiento directo: validador cerrado", "Direct maintenance: validator closed"),
        ("Billetes trabados en la boca de rechazo", "Notes stuck in the reject pocket"),
        ("Prueba de depósito: esperando billetes…", "Deposit test: waiting for notes…"),
        ("Curva derecho / izquierdo y HD trasero", "Turn right / left and rear HD"),
        ("Atasco en el canal de salida al escrow", "Jam in the output channel to escrow"),
        ("TestTool del SP no apunta a esta app: ", "SP TestTool does not point to this app: "),
        ("Mock: cambio de bolsa, contadores en 0", "Mock: bag exchange, counters reset"),
        ("El validador informa una anomalía en ", "The validator reports an anomaly in "),
        ("el validador informa estado de error", "the validator reports an error state"),
        ("EXPORTAR BUNDLE DE DIAGNÓSTICO (ZIP)", "EXPORT DIAGNOSTIC BUNDLE (ZIP)"),
        ("Billete en posición de alimentación", "Note in feed position"),
        ("Puerta de la caja fuerte (sensor): ", "Safe door (sensor): "),
        ("No se encontró el SDK del validador", "Validator SDK not found"),
        ("Abriendo la compuerta de escrow...", "Opening the escrow gate..."),
        ("Cerrando la compuerta de escrow...", "Closing the escrow gate..."),
        ("Operación cancelada por el usuario", "Operation cancelled by the user"),
        ("Atasco en la curva del transporte", "Jam in the transport turn"),
        ("Traba de la compuerta de escrow: ", "Escrow gate lock: "),
        ("Abriendo la compuerta de caída...", "Opening the drop door..."),
        ("Cerrando la compuerta de caída...", "Closing the drop door..."),
        ("ciclo(s) de la compuerta de caída", "drop door cycle(s)"),
        ("Sí = depositar (pasan a la bolsa)", "Yes = deposit (notes go to the bag)"),
        ("Bundle de diagnóstico exportado: ", "Diagnostic bundle exported: "),
        ("Sensores CIS derecho e izquierdo", "CIS right and left sensors"),
        ("Último error informado por XFS: ", "Last error reported by XFS: "),
        ("compuerta de escrow con anomalía", "escrow gate anomaly"),
        ("Reset en curso (puede tardar)...", "Reset in progress (may take a while)..."),
        ("(quedan en la salida de rechazo)", "(left in the reject pocket)"),
        ("SIMULADOR BDM-500 (sin hardware)", "BDM-500 SIMULATOR (no hardware)"),
        ("Lanzados por el SP; VDM estado: ", "Launched by the SP; VDM status: "),
        ("Detección de la boca de rechazo", "Reject pocket detection"),
        ("Motor de alimentación bloqueado", "Feed motor blocked"),
        ("Error de la compuerta de escrow", "Escrow gate error"),
        ("compuerta de caída con anomalía", "drop door anomaly"),
        ("Contador de la boca de rechazo", "Reject pocket counter"),
        ("Billetes trabados en el escrow", "Notes stuck in the escrow"),
        ("Error de la compuerta de caída", "Drop door error"),
        ("Sin errores informados por XFS", "No errors reported by XFS"),
        ("billetes en la boca de entrada", "notes in the input pocket"),
        ("billetes en la boca de rechazo", "notes in the reject pocket"),
        ("Registrando cambio de bolsa...", "Registering bag exchange..."),
        ("el validador devolvió el error", "the validator returned error"),
        ("Sensores antipesca y de caída", "Anti-fishing and drop sensors"),
        ("Traba de la compuerta abierta", "Gate lock open"),
        ("Traba de la compuerta cerrada", "Gate lock closed"),
        ("Error del solenoide de desvío", "Diverter solenoid error"),
        ("hay billetes en el transporte", "notes in the transport"),
        ("Pulse «Leer sensores en vivo»", "Press «Read live sensors»"),
        ("Ya hay un pedido VDM en curso", "A VDM request is already in progress"),
        ("Iniciando lectura en vivo...", "Starting live reading..."),
        ("VDM: sin respuesta del SP a ", "VDM: no SP response to "),
        ("LEER CAPABILITIES DEL EQUIPO", "READ DEVICE CAPABILITIES"),
        ("Compuerta de escrow abierta", "Escrow gate open"),
        ("Compuerta de escrow cerrada", "Escrow gate closed"),
        ("Error del módulo de espesor", "Thickness module error"),
        ("Depósito en escrow completo", "Escrow deposit complete"),
        ("Extra (firmware / template)", "Extra (firmware / template)"),
        ("Compuerta de caída abierta", "Drop door open"),
        ("Compuerta de caída cerrada", "Drop door closed"),
        ("Error del módulo magnético", "Magnetic module error"),
        ("hay billetes en el rechazo", "notes in the reject pocket"),
        ("Sin conexión con el equipo", "No connection to the device"),
        ("compuerta de caída abierta", "drop door open"),
        ("compuerta de caída cerrada", "drop door closed"),
        ("Último error del validador", "Last validator error"),
        ("No = devolver los billetes", "No = return the notes"),
        ("Capabilities del validador", "Validator capabilities"),
        ("Cerrar compuerta de escrow", "Close escrow gate"),
        ("Servicio VDM no disponible", "VDM service not available"),
        ("Motor principal bloqueado", "Main motor blocked"),
        ("Atasco en la alimentación", "Jam in the feed"),
        ("atasco en la alimentación", "jam in the feed"),
        ("hay billetes en el escrow", "notes in the escrow"),
        ("anomalía en una compuerta", "gate anomaly"),
        ("billetes en el transporte", "notes in the transport"),
        ("CERRAR COMPUERTA DE CAÍDA", "CLOSE DROP DOOR"),
        ("SALIR DEL MODO SUPERVISOR", "EXIT SUPERVISOR MODE"),
        ("ENTRAR EN MODO SUPERVISOR", "ENTER SUPERVISOR MODE"),
        ("Devolviendo los billetes…", "Returning the notes…"),
        ("Abrir compuerta de escrow", "Open escrow gate"),
        ("Cerrar compuerta de caída", "Close drop door"),
        ("VDM: registrado a eventos", "VDM: registered for events"),
        ("Pendiente de inicializar", "Pending initialization"),
        ("ABRIR COMPUERTA DE CAÍDA", "OPEN DROP DOOR"),
        ("Error leyendo sensores: ", "Error reading sensors: "),
        ("Abrir compuerta de caída", "Open drop door"),
        ("VDM disponible. Estado: ", "VDM available. Status: "),
        ("Tapa superior levantada", "Top cover lifted"),
        ("hay billetes en la boca", "notes in the hopper"),
        ("atasco en el transporte", "jam in the transport"),
        ("sensor antipesca tapado", "anti-fishing sensor covered"),
        ("Lectura en vivo pausada", "Live reading paused"),
        (" · traba de la puerta: ", " · door lock: "),
        ("Conectando al equipo...", "Connecting to the device..."),
        ("Leyendo capabilities...", "Reading capabilities..."),
        ("Parámetro SP cambiado: ", "SP parameter changed: "),
        ("Depositario de billetes", "Banknote depositor"),
        ("Sensor de alimentación", "Feed sensor"),
        ("VALIDADOR: SIN LECTURA", "VALIDATOR: NO READING"),
        ("Sin atascos ni errores", "No jams or errors"),
        ("la compuerta de escrow", "the escrow gate"),
        ("El validador informa: ", "The validator reports: "),
        ("No conectado al SP XFS", "Not connected to the XFS SP"),
        ("VDM evento de sistema ", "VDM system event "),
        ("XFS Manager iniciado: ", "XFS Manager started: "),
        ("Boca de rechazo llena", "Reject pocket full"),
        ("boca de rechazo llena", "reject pocket full"),
        ("falla de la compuerta", "gate fault"),
        ("billetes en el escrow", "notes in the escrow"),
        ("Lectura de sensores: ", "Sensor reading: "),
        ("MANTENIMIENTO DIRECTO", "DIRECT MAINTENANCE"),
        ("la compuerta de caída", "the drop door"),
        (" completado por el SP", " completed by the SP"),
        ("ABRIR CARPETA DE LOGS", "OPEN LOG FOLDER"),
        ("Detección del escrow", "Escrow detection"),
        ("Error del módulo CIS", "CIS module error"),
        ("VALIDADOR: VERIFICAR", "VALIDATOR: CHECK"),
        ("Aceptador / unidades", "Acceptor / units"),
        ("Prueba de depósito: ", "Deposit test: "),
        ("Exportando bundle...", "Exporting bundle..."),
        ("Abriendo tool SNBC: ", "Opening SNBC tool: "),
        (" billetes contados, ", " notes counted, "),
        ("' abierto (hService=", "' opened (hService="),
        ("Unidades de efectivo", "Cash units"),
        ("Estado del validador", "Validator status"),
        ("Contador del escrow", "Escrow counter"),
        ("Sin lectura en vivo", "No live reading"),
        ("Bolsa en posición: ", "Bag in position: "),
        ("Compuerta de escrow", "Escrow gate"),
        ("SUPERVISOR INACTIVO", "SUPERVISOR INACTIVE"),
        ("Billetes contados: ", "Notes counted: "),
        ("No existe la clave ", "Registry key not found: "),
        ("TestTool reparado: ", "TestTool repaired: "),
        ("Caja fuerte y bolsa", "Safe and bag"),
        ("Operación cancelada", "Operation cancelled"),
        ("Compuerta de caída", "Drop door"),
        ("Bolsa casi llena: ", "Bag almost full: "),
        ("Aleteo terminado: ", "Flutter finished: "),
        ("Prueba de depósito", "Deposit test"),
        ("PRUEBA DE DEPÓSITO", "DEPOSIT TEST"),
        ("Sensor de espesor", "Thickness sensor"),
        ("Atasco de billete", "Note jam"),
        ("Depósito en curso", "Deposit in progress"),
        ("billetes trabados", "notes stuck"),
        ("Leyendo estado...", "Reading status..."),
        ("SUPERVISOR ACTIVO", "SUPERVISOR ACTIVE"),
        ("ACTUALIZAR ESTADO", "REFRESH STATUS"),
        ("Atascos y errores", "Jams and errors"),
        ("Idioma / Language", "Idioma / Language"),
        ("Hopper (entrada)", "Hopper (input)"),
        ("Compuerta escrow", "Escrow gate"),
        (" · bolsa llena: ", " · bag full: "),
        ("Puerta de bóveda", "Safe door"),
        (" proactivo (req ", " proactive (req "),
        ("RESET DEL EQUIPO", "RESET DEVICE"),
        ("Sensores tapados", "Covered sensors"),
        ("Validador (BNE)", "Validator (BNE)"),
        ("Cambio de bolsa", "Bag exchange"),
        ("Mock: conectado", "Mock: connected"),
        ("CAMBIO DE BOLSA", "BAG EXCHANGE"),
        ("Último error: ", "Last error: "),
        (" · vibración: ", " · vibration: "),
        ("escrow abierto", "escrow open"),
        ("escrow cerrado", "escrow closed"),
        ("No monitoreada", "Not monitored"),
        ("Última lectura", "Last reading"),
        ("Atasco / error", "Jam / error"),
        ("CERRAR ESCROW", "CLOSE ESCROW"),
        ("Leer sensores", "Read sensors"),
        ("PROBAR ALETEO", "TEST FLUTTER"),
        ("Tapa frontal", "Front cover"),
        ("Tapa trasera", "Rear cover"),
        ("VERIFICAR · ", "CHECK · "),
        ("sin posición", "no position"),
        ("ABRIR ESCROW", "OPEN ESCROW"),
        ("Rechazados: ", "Rejected: "),
        ("(sin estado)", "(no status)"),
        ("No conectado", "Not connected"),
        ("VDM estado: ", "VDM status: "),
        ("Tapa abierta", "Cover open"),
        ("Herramientas", "Tools"),
        ("VALIDADOR: ", "VALIDATOR: "),
        ("Transporte ", "Transport "),
        ("Sin errores", "No errors"),
        ("Cancelando…", "Cancelling…"),
        ("Validador: ", "Validator: "),
        ("Leer estado", "Read status"),
        (" rechazados", " rejected"),
        ("VDM evento ", "VDM event "),
        ("Reintento ", "Retry "),
        ("Servicio '", "Service '"),
        ("Encendido", "Powered on"),
        ("Por XFS: ", "Via XFS: "),
        ("Lectura: ", "Reading: "),
        ("atasco · ", "jam · "),
        ("Usuario: ", "User: "),
        ("' cerrado", "' closed"),
        ("Cancelado", "Cancelled"),
        ("Aleteo: ", "Flutter: "),
        ("ATASCO. ", "JAM. "),
        ("Equipo: ", "Machine: "),
        (" de 5...", " of 5..."),
        (" (antes ", " (was "),
        ("ATASCO ", "JAM "),
        ("Fecha: ", "Date: "),
        ("evento ", "event "),
        ("Módulos", "Modules"),
        ("REPARAR", "REPAIR"),
        ("SO: ", "OS: "),
    ];
    private static readonly Dictionary<string, string> Whole = new()
    {
        ["Listo"] = "Ready",
        ["Atasco"] = "Jam",
        ["DETECTADO"] = "DETECTED",
        ["Idioma / Language"] = "Idioma / Language",
        ["Error"] = "Error",
        ["Estado"] = "Status",
        ["Libre"] = "Free",
        ["Tapado"] = "Covered",
        ["Cerrada"] = "Closed",
        ["Abierta"] = "Open",
        ["Escrow"] = "Escrow",
        ["Rechazo"] = "Reject",
        ["Transporte"] = "Transport",
        ["Antipesca"] = "Anti-fishing",
        ["Sin lectura"] = "No reading",
        ["Mantenimiento"] = "Maintenance",
        ["Tipo"] = "Type",
        ["Billetes"] = "Notes",
        ["Máx."] = "Max.",
        ["estado "] = "status ",
        ["Error: "] = "Error: ",
        ["CANCELAR"] = "CANCEL",
        ["Cancelar"] = "Cancel",
        ["Equipo"] = "Device",
    };

    public static string T(string? s)
    {
        if (s is null) return "";
        if (Lang != "en" || s.Length == 0) return s;
        if (Whole.TryGetValue(s, out var w)) return w;
        foreach (var (es, en) in Phrases) if (s.Contains(es, StringComparison.Ordinal)) s = s.Replace(es, en, StringComparison.Ordinal);
        return s;
    }

    private static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Feedbill", "BdmSupervisor", "lang.txt");

    public static void Load()
    {
        try { var v = File.ReadAllText(FilePath).Trim().ToLowerInvariant(); if (v is "es" or "en") Lang = v; } catch { }
    }

    public static void Set(string lang)
    {
        if (lang == Lang) return;
        Lang = lang;
        try { Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!); File.WriteAllText(FilePath, lang); } catch { }
        Refresh();
        Changed?.Invoke();
    }

    // ---------- Enganche de controles ----------
    private sealed class Src { public string Text = ""; }
    private static readonly List<(WeakReference<DependencyObject> Obj, DependencyProperty Prop, Src Src)> _hooks = [];
    private static readonly ConditionalWeakTable<DependencyObject, Dictionary<DependencyProperty, Src>> _map = new();
    private static bool _applying;

    /// <summary>Engancha todos los textos de un árbol de controles (TextBlock, botones, pestañas, tooltips).</summary>
    public static void Attach(DependencyObject root)
    {
        switch (root)
        {
            case TextBlock tb: Hook(tb, TextBlock.TextProperty); break;
            case HeaderedContentControl h: if (h.Header is string) Hook(h, HeaderedContentControl.HeaderProperty); if (h.Content is string) Hook(h, ContentControl.ContentProperty); break;
            case ContentControl c when c is not Window: if (c.Content is string) Hook(c, ContentControl.ContentProperty); break;
        }
        if (root is FrameworkElement fe && fe.ToolTip is string) Hook(fe, FrameworkElement.ToolTipProperty);
        if (root is Window w && w.Title is string) Hook(w, Window.TitleProperty);
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()) Attach(child);
    }

    private static void Hook(DependencyObject d, DependencyProperty p)
    {
        var props = _map.GetOrCreateValue(d);
        if (props.ContainsKey(p)) return;
        var src = new Src { Text = d.GetValue(p) as string ?? "" };
        props[p] = src;
        _hooks.Add((new WeakReference<DependencyObject>(d), p, src));
        System.ComponentModel.DependencyPropertyDescriptor.FromProperty(p, d.GetType())?.AddValueChanged(d, (_, _) =>
        {
            if (_applying || d.GetValue(p) is not string v) return;
            src.Text = v; Apply(d, p, src);
        });
        Apply(d, p, src);
    }

    private static void Apply(DependencyObject d, DependencyProperty p, Src src)
    {
        var t = T(src.Text);
        if (Equals(d.GetValue(p), t)) return;
        _applying = true;
        try { d.SetValue(p, t); } finally { _applying = false; }
    }

    public static void Refresh()
    {
        _hooks.RemoveAll(h => !h.Obj.TryGetTarget(out _));
        foreach (var (o, p, s) in _hooks) if (o.TryGetTarget(out var d)) Apply(d, p, s);
    }
}

/// <summary>MessageBox con los textos traducidos al idioma de la interfaz.</summary>
public static class Mb
{
    public static MessageBoxResult Show(string text) => MessageBox.Show(Loc.T(text));
    public static MessageBoxResult Show(string text, string caption) => MessageBox.Show(Loc.T(text), Loc.T(caption));
    public static MessageBoxResult Show(string text, string caption, MessageBoxButton b, MessageBoxImage i) => MessageBox.Show(Loc.T(text), Loc.T(caption), b, i);
    public static MessageBoxResult Show(Window owner, string text, string caption, MessageBoxButton b, MessageBoxImage i) => MessageBox.Show(owner, Loc.T(text), Loc.T(caption), b, i);
}
