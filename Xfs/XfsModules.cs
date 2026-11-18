using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using BdmSupervisor.Services;

namespace BdmSupervisor.Xfs;

/// <summary>Un módulo del equipo tal como se define en appsettings.json (sección "Modules").</summary>
public sealed record ModuleDef(string Type, string LogicalName, string Title);

public sealed record ModuleLine(string Label, string Value, bool? Ok = null);

public sealed class ModuleStatus
{
    public bool Opened { get; init; }
    public int Device { get; init; } = -1;          // fwDevice; -1 = no se pudo abrir el servicio
    public string Error { get; init; } = "";
    public string Sp { get; init; } = "";
    public List<ModuleLine> Lines { get; } = [];
    /// <summary>Luces guía de la SIU: índice XFS, nombre y estado actual.</summary>
    public List<(int Index, string Name, int Value)> Lights { get; } = [];
    public string Extra { get; set; } = "";
    public bool Ok => Opened && Device == 0;
}

public sealed record ModuleTestResult(bool Ok, string Text, string? ImagePath = null);

/// <summary>
/// Lectura de estado y pruebas de los módulos XFS del BDM-500 (impresora, lectoras, pinpad, SIU, cámaras, código de barras).
/// Cada operación abre el servicio lógico, hace lo suyo y lo cierra, para no dejar tomados dispositivos que usa Softy.
/// Las estructuras se leen por desplazamiento (XFS 3.x, empaquetado de 1 byte, proceso de 32 bits).
/// </summary>
public static class XfsModules
{
    // Clases de servicio: desplazamiento base de categorías / comandos
    const uint PTR = 100, IDC = 200, PIN = 400, SIU = 800, CAM = 1000, BCR = 1500;
    const int ResultBuffer = 30;   // WFSRESULT.lpBuffer

    public static readonly string[] DeviceNames = ["EN LÍNEA", "FUERA DE LÍNEA", "APAGADO", "SIN DISPOSITIVO", "ERROR DE HARDWARE", "ERROR DE USUARIO", "OCUPADO"];
    public static string DeviceName(int d) => d >= 0 && d < DeviceNames.Length ? DeviceNames[d] : $"ESTADO {d}";

    /// <summary>Clase XFS del tipo de módulo configurado.</summary>
    public static uint ClassOf(string type) => type.ToUpperInvariant() switch
    {
        "PTR" or "FINGER" => PTR,
        "IDC" or "NFC" => IDC,
        "PIN" => PIN,
        "SIU" => SIU,
        "CAM" => CAM,
        "BCR" => BCR,
        _ => 0,
    };

    public static string TestCaption(string type) => type.ToUpperInvariant() switch
    {
        "PTR" => "IMPRIMIR TICKET DE PRUEBA",
        "IDC" => "LEER TARJETA",
        "NFC" => "LEER TARJETA SIN CONTACTO",
        "PIN" => "PROBAR TECLADO",
        "SIU" => "PROBAR TODAS LAS LUCES",
        "CAM" => "TOMAR FOTO",
        "BCR" => "LEER CÓDIGO",
        _ => "",
    };

    public static string TestHelp(string type) => type.ToUpperInvariant() switch
    {
        "PTR" => "Imprime un ticket corto con fecha, hora y nombre del equipo, y lo corta.",
        "IDC" => "Pulse el botón y pase o inserte una tarjeta de banda magnética (30 s). Los datos se muestran enmascarados.",
        "NFC" => "Pulse el botón y acerque una tarjeta sin contacto al lector (30 s). Se muestra el ATR / UID.",
        "PIN" => "Pulse el botón y presione teclas en el pinpad; termina con ENTER o CANCEL (30 s).",
        "SIU" => "Cada luz se puede encender, hacer titilar o apagar por separado. «Probar todas» las hace titilar 5 segundos y después las apaga.",
        "CAM" => "Toma una foto con cada cámara disponible y muestra la última imagen generada.",
        "BCR" => "Pulse el botón y acerque un código de barras o QR al lector (20 s).",
        _ => "Este módulo solo informa su estado.",
    };

    // ---------------- Estado ----------------

    public static ModuleStatus ReadStatus(ModuleDef m, AppConfig cfg, LogService log)
    {
        uint cls = ClassOf(m.Type);
        XfsSession? s = null;
        try
        {
            s = new XfsSession(m.LogicalName, cfg.AppId, 8000, cfg.TraceLevel, log);
        }
        catch (XfsException ex)
        {
            return new ModuleStatus { Opened = false, Error = XfsConst.ErrorName(ex.HResult2) };
        }
        catch (Exception ex)
        {
            return new ModuleStatus { Opened = false, Error = ex.Message };
        }
        using (s)
        {
            IntPtr res = IntPtr.Zero;
            try
            {
                res = s.GetInfo(cls + 1);
                IntPtr b = Marshal.ReadIntPtr(res, ResultBuffer);
                if (b == IntPtr.Zero) return new ModuleStatus { Opened = true, Error = "Estado vacío", Sp = s.SpDescription };
                int dev = W(b, 0);
                var st = new ModuleStatus { Opened = true, Device = dev, Sp = $"{s.SpDescription} v{s.SpVersion}" };
                st.Lines.Add(new ModuleLine("Dispositivo", DeviceName(dev), dev == 0));
                int extraOff = DecodeStatus(m.Type, b, st);
                if (extraOff >= 0 && Readable(b, extraOff + 4) >= extraOff + 4) st.Extra = ReadExtra(Marshal.ReadIntPtr(b, extraOff));
                log.Info($"[{m.LogicalName}] status raw ({extraOff + 4} bytes): {Hex(b, Math.Max(extraOff + 4, 16))}");
                // Fuera de línea: los valores de los componentes no son confiables, se muestran sin color
                if (dev != 0) for (int i = 1; i < st.Lines.Count; i++) if (st.Lines[i].Ok == true) st.Lines[i] = st.Lines[i] with { Ok = null };
                return st;
            }
            catch (XfsException ex)
            {
                return new ModuleStatus { Opened = true, Error = XfsConst.ErrorName(ex.HResult2), Sp = s.SpDescription };
            }
            finally { XfsSession.FreeResult(res); }
        }
    }

    static readonly string[] PtrMedia = ["Papel en la salida", "Sin papel en la salida", "ATASCADO", "No soportado", "Desconocido", "Entrando", "Retraído"];
    static readonly string[] PtrPaper = ["Lleno", "Bajo", "SIN PAPEL", "No soportado", "Desconocido", "ATASCADO"];
    static readonly string[] Supply = ["Lleno", "Bajo", "VACÍO", "No soportado", "Desconocido"];
    static readonly string[] IdcMedia = ["", "Tarjeta adentro", "Sin tarjeta", "ATASCADA", "No soportado", "Desconocido", "Entrando", "Retenida en la boca"];
    static readonly string[] PinEnc = ["Lista", "NO LISTA", "SIN INICIALIZAR", "Ocupada", "Indefinida", "Inicializada"];
    static readonly string[] CamState = ["No soportada", "OK", "INOPERATIVA", "Desconocido"];
    static readonly string[] CamMedia = ["OK", "Casi llena", "LLENA", "Desconocido", "No soportado"];
    static readonly string[] CamNames = ["Cámara de ambiente", "Cámara de cliente", "Cámara de salida"];
    static readonly string[] BcrScanner = ["Encendido", "Apagado", "INOPERATIVO", "Desconocido"];
    public static readonly string[] SiuSensorNames = ["Llave de operador", "Antisabotaje", "Antisabotaje interno", "Sísmico", "Calor", "Proximidad", "Luz ambiente", "Audio", "Botón de arranque", "Pantalla del cliente", "Botón de llamada", "Auricular", "Entrada general"];
    public static readonly string[] SiuDoorNames = ["Puerta del gabinete", "Puerta de la caja fuerte", "Protector antivandálico"];
    public static readonly string[] SiuIndicatorNames = ["Indicador abierto/cerrado", "Luz de frente", "Audio", "Calefacción", "Volumen de auriculares"];
    public static readonly string[] GuideLightNames = ["Lectora de tarjeta", "Pinpad", "Dispensador", "Monedas", "Impresora de recibos", "Impresora de libretas", "Depósito de sobres", "Cheques", "Aceptador de billetes", "Dispensador de sobres", "Impresora de documentos", "Aceptador de monedas", "Escáner"];

    static string Pick(string[] names, int v) => v >= 0 && v < names.Length && names[v].Length > 0 ? names[v] : $"0x{v:X4}";
    static bool Bad(string s) => s.Length > 2 && s == s.ToUpperInvariant() && s.Any(char.IsLetter);

    /// <summary>Agrega las líneas propias de cada clase y devuelve el desplazamiento de lpszExtra (-1 si no hay).</summary>
    static int DecodeStatus(string type, IntPtr b, ModuleStatus st)
    {
        void L(string label, string v) => st.Lines.Add(new ModuleLine(label, v, Bad(v) ? false : v is "Desconocido" or "No soportado" or "Sin dato" || v.StartsWith("0x") ? null : true));
        switch (ClassOf(type))
        {
            case PTR:
                if (type.Equals("FINGER", StringComparison.OrdinalIgnoreCase)) return 48;
                L("Papel", Pick(PtrPaper, W(b, 4)));
                L("Ticket en la salida", Pick(PtrMedia, W(b, 2)));
                int toner = W(b, 36); if (toner != 3) L("Tóner", Pick(Supply, toner));
                int ink = W(b, 38); if (ink != 3) L("Tinta", Pick(Supply, ink));
                return 48;
            case IDC:
                L("Tarjeta", Pick(IdcMedia, W(b, 2)));
                int cards = W(b, 8); if (cards > 0) L("Tarjetas retenidas", cards.ToString());
                return 12;
            case PIN:
                L("Encriptación", Pick(PinEnc, W(b, 2)));
                return 4;
            case SIU:
            {
                if (Readable(b, 198) < 198) return -1;
                int extra = SiuExtraOffset(b), lights = extra == 194 ? 16 : 32;
                // SP de SNBC: 0 = el elemento no existe; 1 = existe pero sin estado informado
                for (int i = 0; i < SiuDoorNames.Length; i++) { int v = WB(b, 66 + 2 * i); if (v >= 1) L(SiuDoorNames[i], SiuDoor(v)); }
                for (int i = 0; i < SiuSensorNames.Length; i++) { int v = WB(b, 2 + 2 * i); if (v >= 1) st.Lines.Add(new ModuleLine(SiuSensorNames[i], SiuOnOff(v))); }
                for (int i = 0; i < SiuIndicatorNames.Length; i++) { int v = WB(b, 98 + 2 * i); if (v >= 1) st.Lines.Add(new ModuleLine(SiuIndicatorNames[i], SiuOnOff(v))); }
                for (int i = 0; i < lights; i++) { int v = WB(b, 162 + 2 * i); if (v >= 1) st.Lights.Add((i, i < GuideLightNames.Length ? GuideLightNames[i] : $"Luz {i}", v)); }
                return extra;
            }
            case CAM:
                for (int i = 0; i < 3; i++)
                {
                    int c = WB(b, 18 + 2 * i);
                    if (c == 0) continue;
                    string media = Pick(CamMedia, WB(b, 2 + 2 * i));
                    st.Lines.Add(new ModuleLine(CamNames[i], $"{Pick(CamState, c)} · fotos: {WB(b, 34 + 2 * i)} · almacenamiento: {media}", c == 1 && !Bad(media) ? true : c == 2 ? false : null));
                }
                return 50;
            case BCR:
                L("Escáner", Pick(BcrScanner, W(b, 2)));
                return 132;
        }
        return -1;
    }

    /// <summary>
    /// Desplazamiento de lpszExtra en WFSSIUSTATUS: 194 si el SP usa 16 luces guía (XFS 3.0x, como el SIU_STM de SNBC),
    /// 226 si usa 32 (XFS 3.10+). Se decide mirando si en 194 hay un puntero a texto "clave=valor".
    /// </summary>
    static int SiuExtraOffset(IntPtr b)
    {
        IntPtr p = Marshal.ReadIntPtr(b, 194);
        byte[] t = SafeBytes(p, 64);
        int n = Array.IndexOf(t, (byte)0);
        int eq = Array.IndexOf(t, (byte)'=');
        if (n > 2 && eq > 0 && eq < n && t.Take(n).All(c => (c >= 0x20 && c < 0x7F) || c >= 0xA0)) return 194;
        return 226;
    }

    public static string SiuDoor(int v) => v switch { 1 => "Sin dato", 4 => "Cerrada", 8 => "ABIERTA", 16 => "Cerrada con llave", 32 => "Cerrada con cerrojo", 64 => "FORZADA", _ => $"0x{v:X4}" };
    public static string SiuOnOff(int v) => v switch { 1 => "Sin dato", 2 => "Inactivo", 4 => "Activo", _ => $"0x{v:X4}" };
    public static string SiuLight(int v) => v switch { 1 => "Apagada", 2 => "Apagada", 4 => "Titilando lento", 8 => "Titilando", 16 => "Titilando rápido", 0x80 => "Encendida", _ => $"0x{v:X4}" };

    // ---------------- Pruebas ----------------

    public static ModuleTestResult Test(ModuleDef m, AppConfig cfg, LogService log)
    {
        using var s = new XfsSession(m.LogicalName, cfg.AppId, 15000, cfg.TraceLevel, log);
        return m.Type.ToUpperInvariant() switch
        {
            "PTR" => TestPrinter(s),
            "IDC" => TestCard(s, 0x0007, chip: false),
            "NFC" => TestCard(s, 0x0008, chip: true),
            "PIN" => TestPinpad(s),
            "SIU" => TestGuideLights(s),
            "CAM" => TestCamera(s, cfg),
            "BCR" => TestBarcode(s),
            _ => new ModuleTestResult(false, "Este módulo no tiene prueba."),
        };
    }

    static ModuleTestResult TestPrinter(XfsSession s)
    {
        string host = Environment.MachineName;
        string text = "\n      FEEDBILL - BDM SUPERVISOR\n          TICKET DE PRUEBA\n\n" +
                      $"Fecha: {DateTime.Now:dd/MM/yyyy HH:mm:ss}\nEquipo: {host}\n" +
                      "--------------------------------\nImpresora OK\n\n\n\n\n";
        byte[] data = Encoding.ASCII.GetBytes(text);
        string how;
        IntPtr pData = Marshal.AllocHGlobal(data.Length), cmd = Marshal.AllocHGlobal(10);
        try
        {
            Marshal.Copy(data, 0, pData, data.Length);
            Marshal.WriteInt16(cmd, 0, 0);               // wInputData = WFS_PTR_NOINPUTDATA
            Marshal.WriteInt32(cmd, 2, data.Length);     // ulSize
            Marshal.WriteIntPtr(cmd, 6, pData);          // lpbData
            XfsSession.FreeResult(s.Execute(PTR + 4, cmd, 20000));   // WFS_CMD_PTR_RAW_DATA
            how = "RAW_DATA";
        }
        catch (XfsException ex)
        {
            // Algunos SP no aceptan datos crudos: se imprime el formulario de ejemplo instalado por SNBC
            try { PrintForm(s); how = "PRINT_FORM (RAW_DATA: " + XfsConst.ErrorName(ex.HResult2) + ")"; }
            catch (XfsException ex2) { return new ModuleTestResult(false, $"No se pudo imprimir: RAW_DATA {XfsConst.ErrorName(ex.HResult2)} · PRINT_FORM {XfsConst.ErrorName(ex2.HResult2)}"); }
        }
        finally { Marshal.FreeHGlobal(pData); Marshal.FreeHGlobal(cmd); }

        string cut = "corte OK";
        IntPtr ctl = Marshal.AllocHGlobal(4);
        try { Marshal.WriteInt32(ctl, 0x0010 | 0x0004); XfsSession.FreeResult(s.Execute(PTR + 1, ctl, 15000)); } // CONTROL_MEDIA FLUSH|CUT
        catch (XfsException ex) { cut = "corte: " + XfsConst.ErrorName(ex.HResult2); }
        finally { Marshal.FreeHGlobal(ctl); }
        return new ModuleTestResult(true, $"Ticket de prueba enviado ({how}, {cut}).");
    }

    static void PrintForm(XfsSession s)
    {
        string fields = $"Title=FEEDBILL BDM SUPERVISOR\0Title1=TICKET DE PRUEBA\0TransactionType={DateTime.Now:dd/MM/yyyy HH:mm}\0\0";
        IntPtr form = Marshal.StringToHGlobalAnsi("SampleReceiptForm"), media = Marshal.StringToHGlobalAnsi("SampleReceiptMedia");
        byte[] fb = Encoding.ASCII.GetBytes(fields);
        IntPtr pf = Marshal.AllocHGlobal(fb.Length), cmd = Marshal.AllocHGlobal(30);
        try
        {
            Marshal.Copy(fb, 0, pf, fb.Length);
            for (int i = 0; i < 30; i++) Marshal.WriteByte(cmd, i, 0);
            Marshal.WriteIntPtr(cmd, 0, form);
            Marshal.WriteIntPtr(cmd, 4, media);
            Marshal.WriteInt32(cmd, 16, 0x0010 | 0x0004);   // dwMediaControl FLUSH|CUT
            Marshal.WriteIntPtr(cmd, 20, pf);
            XfsSession.FreeResult(s.Execute(PTR + 2, cmd, 20000));   // WFS_CMD_PTR_PRINT_FORM
        }
        finally { Marshal.FreeHGlobal(form); Marshal.FreeHGlobal(media); Marshal.FreeHGlobal(pf); Marshal.FreeHGlobal(cmd); }
    }

    static ModuleTestResult TestCard(XfsSession s, ushort sources, bool chip)
    {
        IntPtr cmd = Marshal.AllocHGlobal(2), res = IntPtr.Zero;
        var sb = new StringBuilder();
        bool ok = false;
        try
        {
            Marshal.WriteInt16(cmd, 0, (short)sources);
            res = s.Execute(IDC + 7, cmd, 30000);       // WFS_CMD_IDC_READ_RAW_DATA
            IntPtr arr = Marshal.ReadIntPtr(res, ResultBuffer);
            for (int i = 0; arr != IntPtr.Zero && i < 16; i++)
            {
                IntPtr cd = Marshal.ReadIntPtr(arr, i * IntPtr.Size);
                if (cd == IntPtr.Zero) break;
                int src = W(cd, 0), status = W(cd, 2), len = Marshal.ReadInt32(cd, 4);
                IntPtr data = Marshal.ReadIntPtr(cd, 8);
                byte[] bytes = len > 0 && data != IntPtr.Zero ? new byte[Math.Min(len, 4096)] : [];
                if (bytes.Length > 0) Marshal.Copy(data, bytes, 0, bytes.Length);
                string name = src switch { 1 => "Pista 1", 2 => "Pista 2", 4 => "Pista 3", 8 => chip ? "ATR / UID" : "Chip", _ => $"Fuente 0x{src:X}" };
                string st = status switch { 0 => "OK", 1 => "faltante", 2 => "inválida", 3 => "muy larga", 4 => "muy corta", 5 => "no soportada", _ => $"estado {status}" };
                string val = chip ? BitConverter.ToString(bytes).Replace("-", " ") : Mask(Encoding.ASCII.GetString(bytes));
                sb.AppendLine($"{name}: {st} · {bytes.Length} bytes{(bytes.Length > 0 ? " · " + val : "")}");
                if (status == 0 && bytes.Length > 0) ok = true;
            }
            if (sb.Length == 0) sb.AppendLine("El lector respondió sin datos.");
        }
        catch (XfsException ex)
        {
            sb.AppendLine(ex.HResult2 == XfsConst.WFS_ERR_TIMEOUT ? "No se presentó ninguna tarjeta (tiempo agotado)." : "Error de lectura: " + XfsConst.ErrorName(ex.HResult2));
        }
        finally { XfsSession.FreeResult(res); Marshal.FreeHGlobal(cmd); }
        if (!chip) { try { XfsSession.FreeResult(s.Execute(IDC + 3, IntPtr.Zero, 15000, quiet: true)); } catch (XfsException) { } } // EJECT_CARD
        return new ModuleTestResult(ok, sb.ToString().TrimEnd());
    }

    /// <summary>Enmascara los datos de pista: se ven los primeros 6 y los últimos 4 dígitos del número.</summary>
    static string Mask(string t)
    {
        var sb = new StringBuilder(t.Length);
        int digits = t.Count(char.IsDigit), seen = 0;
        foreach (char c in t)
        {
            if (!char.IsDigit(c)) { sb.Append(char.IsControl(c) ? ' ' : c); continue; }
            seen++;
            sb.Append(seen <= 6 || seen > digits - 4 ? c : '*');
        }
        return sb.ToString().Trim();
    }

    static readonly (uint Key, string Name)[] PinKeys =
    [
        (0x1, "0"), (0x2, "1"), (0x4, "2"), (0x8, "3"), (0x10, "4"), (0x20, "5"), (0x40, "6"), (0x80, "7"), (0x100, "8"), (0x200, "9"),
        (0x400, "ENTER"), (0x800, "CANCEL"), (0x1000, "CLEAR"), (0x2000, "BACKSPACE"), (0x4000, "HELP"), (0x8000, "."), (0x10000, "00"), (0x20000, "000"),
    ];

    static ModuleTestResult TestPinpad(XfsSession s)
    {
        IntPtr cmd = Marshal.AllocHGlobal(22), res = IntPtr.Zero;
        try
        {
            Marshal.WriteInt16(cmd, 0, 16);                 // usMaxLen
            Marshal.WriteInt32(cmd, 2, 0);                  // bAutoEnd
            Marshal.WriteInt32(cmd, 6, 0);                  // ulActiveFDKs
            Marshal.WriteInt32(cmd, 10, 0x3FFFF);           // ulActiveKeys: 0-9, ENTER, CANCEL, CLEAR, BACKSPACE, HELP, ., 00, 000
            Marshal.WriteInt32(cmd, 14, 0);                 // ulTerminateFDKs
            Marshal.WriteInt32(cmd, 18, 0x400 | 0x800);     // ulTerminateKeys: ENTER, CANCEL
            res = s.Execute(PIN + 9, cmd, 30000);           // WFS_CMD_PIN_GET_DATA
            IntPtr b = Marshal.ReadIntPtr(res, ResultBuffer);
            var keys = new List<string>();
            if (b != IntPtr.Zero)
            {
                int n = W(b, 0);
                IntPtr arr = Marshal.ReadIntPtr(b, 2);
                for (int i = 0; i < n && arr != IntPtr.Zero && i < 64; i++)
                {
                    IntPtr k = Marshal.ReadIntPtr(arr, i * IntPtr.Size);
                    if (k == IntPtr.Zero) break;
                    uint digit = (uint)Marshal.ReadInt32(k, 2);
                    keys.Add(PinKeys.FirstOrDefault(p => p.Key == digit).Name ?? $"0x{digit:X}");
                }
            }
            return keys.Count == 0
                ? new ModuleTestResult(false, "El pinpad terminó sin teclas.")
                : new ModuleTestResult(true, "Teclas recibidas: " + string.Join(" ", keys));
        }
        catch (XfsException ex)
        {
            return new ModuleTestResult(false, ex.HResult2 == XfsConst.WFS_ERR_TIMEOUT ? "No se presionó ninguna tecla (tiempo agotado)." : "Error del pinpad: " + XfsConst.ErrorName(ex.HResult2));
        }
        finally { XfsSession.FreeResult(res); Marshal.FreeHGlobal(cmd); }
    }

    static ModuleTestResult TestGuideLights(XfsSession s)
    {
        var avail = new List<int>();
        IntPtr res = IntPtr.Zero;
        try
        {
            res = s.GetInfo(SIU + 1);
            IntPtr b = Marshal.ReadIntPtr(res, ResultBuffer);
            int lights = SiuExtraOffset(b) == 194 ? 16 : 32;
            for (int i = 0; i < lights; i++) if (WB(b, 162 + 2 * i) >= 1) avail.Add(i);
        }
        finally { XfsSession.FreeResult(res); }
        if (avail.Count == 0) return new ModuleTestResult(false, "La placa SIU no informa luces guía disponibles.");

        var done = new List<string>(); var failed = new List<string>();
        foreach (int i in avail) (SetLight(s, i, 0x0004) ? done : failed).Add(i < GuideLightNames.Length ? GuideLightNames[i] : $"#{i}");
        Thread.Sleep(5000);
        foreach (int i in avail) SetLight(s, i, 0x0002);
        string txt = done.Count > 0 ? "Titilaron y se apagaron: " + string.Join(", ", done) : "";
        if (failed.Count > 0) txt += (txt.Length > 0 ? "\n" : "") + "Sin respuesta: " + string.Join(", ", failed);
        return new ModuleTestResult(done.Count > 0, txt);
    }

    public const int LightOff = 0x0002, LightFlash = 0x0008, LightOn = 0x0080;

    /// <summary>Enciende, hace titilar o apaga una luz guía de la SIU.</summary>
    public static ModuleTestResult SetGuideLight(ModuleDef m, AppConfig cfg, LogService log, int light, int command)
    {
        using var s = new XfsSession(m.LogicalName, cfg.AppId, 15000, cfg.TraceLevel, log);
        string name = light < GuideLightNames.Length ? GuideLightNames[light] : $"Luz {light}";
        string what = command switch { LightOn => "encendida", LightFlash => "titilando", _ => "apagada" };
        return SetLight(s, light, command)
            ? new ModuleTestResult(true, $"{name}: {what}")
            : new ModuleTestResult(false, $"{name}: la SIU no aceptó el comando");
    }

    static bool SetLight(XfsSession s, int light, int command)
    {
        IntPtr cmd = Marshal.AllocHGlobal(4);
        try
        {
            Marshal.WriteInt16(cmd, 0, (short)light);
            Marshal.WriteInt16(cmd, 2, (short)command);
            XfsSession.FreeResult(s.Execute(SIU + 6, cmd, 10000, quiet: true));   // WFS_CMD_SIU_SET_GUIDLIGHT
            return true;
        }
        catch (XfsException) { return false; }
        finally { Marshal.FreeHGlobal(cmd); }
    }

    static ModuleTestResult TestCamera(XfsSession s, AppConfig cfg)
    {
        var cams = new List<int>();
        IntPtr res = IntPtr.Zero;
        try
        {
            res = s.GetInfo(CAM + 1);
            IntPtr b = Marshal.ReadIntPtr(res, ResultBuffer);
            for (int i = 0; i < 3; i++) if (WB(b, 18 + 2 * i) is 1) cams.Add(i);
        }
        catch (XfsException) { }
        finally { XfsSession.FreeResult(res); }
        if (cams.Count == 0) cams.Add(0);

        DateTime start = DateTime.Now.AddSeconds(-2);
        var sb = new StringBuilder(); bool ok = false;
        IntPtr text = Marshal.StringToHGlobalAnsi($"Feedbill BDM Supervisor {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
        foreach (int c in cams)
        {
            IntPtr cmd = Marshal.AllocHGlobal(10);
            try
            {
                Marshal.WriteInt16(cmd, 0, (short)c);
                Marshal.WriteIntPtr(cmd, 2, text);
                Marshal.WriteIntPtr(cmd, 6, IntPtr.Zero);
                XfsSession.FreeResult(s.Execute(CAM + 1, cmd, 20000));   // WFS_CMD_CAM_TAKE_PICTURE
                sb.AppendLine($"{CamNames[c]}: foto tomada");
                ok = true;
            }
            catch (XfsException ex) { sb.AppendLine($"{CamNames[c]}: {XfsConst.ErrorName(ex.HResult2)}"); }
            finally { Marshal.FreeHGlobal(cmd); }
        }
        Marshal.FreeHGlobal(text);

        string? img = ok ? NewestImage(cfg, start) : null;
        if (ok) sb.AppendLine(img is null ? "No se encontró el archivo de la imagen (configure Paths.CameraImageDir)." : "Imagen: " + img);
        return new ModuleTestResult(ok, sb.ToString().TrimEnd(), img);
    }

    static string? NewestImage(AppConfig cfg, DateTime since)
    {
        var dirs = new List<string>();
        if (!string.IsNullOrWhiteSpace(cfg.CameraImageDir)) dirs.Add(cfg.CameraImageDir!);
        dirs.Add(Path.Combine(SpParamsService.SpDir, "CAM_SNBC_Standard"));
        dirs.Add(@"C:\CameraPicture"); dirs.Add(@"C:\Pictures"); dirs.Add(@"C:\snbclogs");
        string[] ext = [".jpg", ".jpeg", ".bmp", ".png"];
        for (int attempt = 0; attempt < 6; attempt++)
        {
            var f = dirs.Where(Directory.Exists)
                .SelectMany(d => { try { return Directory.EnumerateFiles(d, "*.*", SearchOption.AllDirectories); } catch { return []; } })
                .Where(p => ext.Contains(Path.GetExtension(p).ToLowerInvariant()))
                .Select(p => new FileInfo(p)).Where(fi => fi.LastWriteTime >= since)
                .OrderByDescending(fi => fi.LastWriteTime).FirstOrDefault();
            if (f is not null) return f.FullName;
            Thread.Sleep(500);
        }
        return null;
    }

    static ModuleTestResult TestBarcode(XfsSession s)
    {
        IntPtr cmd = Marshal.AllocHGlobal(4), res = IntPtr.Zero;
        try
        {
            Marshal.WriteIntPtr(cmd, 0, IntPtr.Zero);      // lpwSymbologies = NULL: todas
            res = s.Execute(BCR + 1, cmd, 20000);          // WFS_CMD_BCR_READ
            IntPtr arr = Marshal.ReadIntPtr(res, ResultBuffer);
            var sb = new StringBuilder();
            for (int i = 0; arr != IntPtr.Zero && i < 16; i++)
            {
                IntPtr o = Marshal.ReadIntPtr(arr, i * IntPtr.Size);
                if (o == IntPtr.Zero) break;
                IntPtr x = Marshal.ReadIntPtr(o, 2);
                string code = "";
                if (x != IntPtr.Zero)
                {
                    int len = W(x, 0); IntPtr d = Marshal.ReadIntPtr(x, 2);
                    if (len > 0 && d != IntPtr.Zero) { var bytes = new byte[Math.Min(len, 4096)]; Marshal.Copy(d, bytes, 0, bytes.Length); code = Encoding.UTF8.GetString(bytes); }
                }
                IntPtr nm = Marshal.ReadIntPtr(o, 6);
                string sym = nm != IntPtr.Zero ? Marshal.PtrToStringAnsi(nm) ?? "" : $"simbología {W(o, 0)}";
                sb.AppendLine($"{sym}: {code}");
            }
            return sb.Length == 0 ? new ModuleTestResult(false, "El lector respondió sin datos.") : new ModuleTestResult(true, sb.ToString().TrimEnd());
        }
        catch (XfsException ex)
        {
            return new ModuleTestResult(false, ex.HResult2 == XfsConst.WFS_ERR_TIMEOUT ? "No se leyó ningún código (tiempo agotado)." : "Error del lector: " + XfsConst.ErrorName(ex.HResult2));
        }
        finally { XfsSession.FreeResult(res); Marshal.FreeHGlobal(cmd); }
    }

    // ---------------- utilidades ----------------
    static int W(IntPtr p, int off) => (ushort)Marshal.ReadInt16(p, off);
    /// <summary>Algunos SP llenan los arreglos con memset (p. ej. 0x0303 = 3 en cada byte): se toma el byte bajo.</summary>
    static int WB(IntPtr p, int off) { int v = W(p, off); return v > 0xFF && (v >> 8) == (v & 0xFF) ? v & 0xFF : v; }

    [DllImport("kernel32.dll")]
    static extern UIntPtr VirtualQuery(IntPtr lpAddress, out MEMINFO lpBuffer, UIntPtr dwLength);
    [StructLayout(LayoutKind.Sequential)]
    struct MEMINFO { public IntPtr BaseAddress, AllocationBase; public uint AllocationProtect; public UIntPtr RegionSize; public uint State, Protect, Type; }

    /// <summary>Bytes legibles a partir de p (0 si el puntero no apunta a memoria válida). Evita que un SP con estructuras distintas cierre la app.</summary>
    static int Readable(IntPtr p, int max)
    {
        if (p == IntPtr.Zero || (ulong)p.ToInt64() < 0x10000) return 0;
        if (VirtualQuery(p, out var mi, (UIntPtr)Marshal.SizeOf<MEMINFO>()) == UIntPtr.Zero) return 0;
        const uint Commit = 0x1000, Guard = 0x100, NoAccess = 0x01;
        if (mi.State != Commit || (mi.Protect & (Guard | NoAccess)) != 0 || mi.Protect == 0) return 0;
        long end = mi.BaseAddress.ToInt64() + (long)mi.RegionSize.ToUInt64();
        return (int)Math.Min(max, end - p.ToInt64());
    }

    static byte[] SafeBytes(IntPtr p, int max)
    {
        int n = Readable(p, max);
        var b = new byte[Math.Max(n, 0)];
        if (n > 0) Marshal.Copy(p, b, 0, n);
        return b;
    }

    static string Hex(IntPtr p, int len) => BitConverter.ToString(SafeBytes(p, len)).Replace("-", "");

    static string ReadExtra(IntPtr p)
    {
        byte[] b = SafeBytes(p, 8192);
        if (b.Length == 0) return "";
        var parts = new List<string>();
        int start = 0;
        for (int i = 0; i < b.Length; i++)
        {
            if (b[i] != 0) continue;
            if (i == start) break;                 // doble nulo: fin de la lista
            string s = Encoding.Latin1.GetString(b, start, i - start);
            if (s.Any(c => c < 0x20 && c != 0x09)) break;   // basura: no es una lista de texto
            parts.Add(s);
            start = i + 1;
        }
        return string.Join("  ·  ", parts);
    }
}
