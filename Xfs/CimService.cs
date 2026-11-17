using System.Runtime.InteropServices;
using BdmSupervisor.Services;

namespace BdmSupervisor.Xfs;

/// <summary>Estado parseado de WFS_INF_CIM_STATUS.</summary>
public sealed record CimStatus(
    string Device, string SafeDoor, string Acceptor, string IntermediateStacker, string StackerItems,
    string BanknoteReader, bool DropBox, IReadOnlyList<CimPosition> Positions, string Extra, DateTime Timestamp);

public sealed record CimPosition(string Position, string Shutter, string PositionStatus, string Transport, string TransportStatus);

/// <summary>Billetes contados de un tipo en una operación de depósito.</summary>
public sealed record CimNoteCount(int NoteId, string Currency, long Value, int Count);

/// <summary>Una unidad lógica/física de WFS_INF_CIM_CASH_UNIT_INFO (bolsa, escrow, reject...).</summary>
public sealed record CimCashUnit(int Number, string Type, string UnitId, int Count, int CashInCount, int Maximum, string Status);

/// <summary>
/// Wrapper del servicio CIM (Cash-In Module) sobre XFS.
/// Los layouts de las estructuras siguen CEN/XFS CIM 3.x (Pack=1).
/// </summary>
public sealed class CimService : IDisposable
{
    private readonly XfsSession _s;
    private readonly LogService _log;

    public CimService(XfsSession session, LogService log) { _s = session; _log = log; }

    public string SpDescription => _s.SpDescription;
    public string SpVersion => _s.SpVersion;

    // WFSCIMSTATUS (CIM 3.x), primeros campos:
    // WORD fwDevice; WORD fwSafeDoor; WORD fwAcceptor; WORD fwIntermediateStacker; WORD fwStackerItems;
    // WORD fwBanknoteReader; BOOL bDropBox; LPWFSCIMINPOS *lppPositions; LPSTR lpszExtra; ...
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct WFSCIMSTATUS
    {
        public ushort fwDevice, fwSafeDoor, fwAcceptor, fwIntermediateStacker, fwStackerItems, fwBanknoteReader;
        public int bDropBox;
        public IntPtr lppPositions;
        public IntPtr lpszExtra;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct WFSCIMINPOS
    {
        public ushort fwPosition, fwShutter, fwPositionStatus, fwTransport, fwTransportStatus;
    }

    // WFSCIMCASHINFO: USHORT usCount; LPWFSCIMCASHIN *lppCashIn;
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct WFSCIMCASHINFO { public ushort usCount; public IntPtr lppCashIn; }

    // WFSCIMCASHIN (prefijo): USHORT usNumber; DWORD fwType; DWORD fwItemType; CHAR cUnitID[5]; CHAR cCurrencyID[3];
    // ULONG ulValues; ULONG ulCashInCount; ULONG ulCount; ULONG ulMaximum; USHORT usStatus; ...
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct WFSCIMCASHIN
    {
        public ushort usNumber;
        public uint fwType;
        public uint fwItemType;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)] public byte[] cUnitID;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)] public byte[] cCurrencyID;
        public uint ulValues, ulCashInCount, ulCount, ulMaximum;
        public ushort usStatus;
    }

    public CimStatus GetStatus()
    {
        var r = _s.GetInfo(XfsConst.WFS_INF_CIM_STATUS);
        try
        {
            var res = XfsSession.ReadResult(r);
            var st = Marshal.PtrToStructure<WFSCIMSTATUS>(res.lpBuffer);
            var positions = new List<CimPosition>();
            if (st.lppPositions != IntPtr.Zero)
            {
                for (int i = 0; ; i++)
                {
                    var p = Marshal.ReadIntPtr(st.lppPositions, i * IntPtr.Size);
                    if (p == IntPtr.Zero) break;
                    var pos = Marshal.PtrToStructure<WFSCIMINPOS>(p);
                    positions.Add(new CimPosition(
                        pos.fwPosition switch { 0x0001 => "INLEFT", 0x0002 => "INRIGHT", 0x0004 => "INCENTER", 0x0008 => "INTOP",
                            0x0010 => "INBOTTOM", 0x0020 => "INFRONT", 0x0040 => "INREAR", 0x0080 => "OUTLEFT", 0x0100 => "OUTRIGHT",
                            0x0200 => "OUTCENTER", 0x0400 => "OUTTOP", 0x0800 => "OUTBOTTOM", 0x1000 => "OUTFRONT", 0x2000 => "OUTREAR",
                            _ => $"POS{pos.fwPosition}" },
                        XfsConst.Name(XfsConst.ShutterNames, pos.fwShutter),
                        XfsConst.Name(XfsConst.PosStatusNames, pos.fwPositionStatus),
                        XfsConst.Name(XfsConst.TransportNames, pos.fwTransport),
                        pos.fwTransportStatus switch { 0 => "EMPTY", 1 => "NOTEMPTY", 2 => "NOTEMPTYCUST", 3 => "NOTEMPTY_UNK", 4 => "NOTSUPP", _ => "?" }));
                }
            }
            string extra = st.lpszExtra != IntPtr.Zero ? ReadMultiSz(st.lpszExtra) : "";
            return new CimStatus(
                XfsConst.Name(XfsConst.DeviceNames, st.fwDevice),
                XfsConst.Name(XfsConst.SafeDoorNames, st.fwSafeDoor),
                XfsConst.Name(XfsConst.AcceptorNames, st.fwAcceptor),
                XfsConst.Name(XfsConst.StackerNames, st.fwIntermediateStacker),
                st.fwStackerItems switch { 0 => "CUSTOMERACCESS", 1 => "NOCUSTOMERACCESS", 2 => "ACCESSUNKNOWN", 4 => "NOITEMS", _ => "?" },
                XfsConst.Name(XfsConst.BnrNames, st.fwBanknoteReader),
                st.bDropBox != 0, positions, extra, res.tsTimestamp.ToDateTime());
        }
        finally { XfsSession.FreeResult(r); }
    }

    public IReadOnlyList<CimCashUnit> GetCashUnits()
    {
        var r = _s.GetInfo(XfsConst.WFS_INF_CIM_CASH_UNIT_INFO);
        try
        {
            var res = XfsSession.ReadResult(r);
            var info = Marshal.PtrToStructure<WFSCIMCASHINFO>(res.lpBuffer);
            var list = new List<CimCashUnit>();
            for (int i = 0; i < info.usCount; i++)
            {
                var p = Marshal.ReadIntPtr(info.lppCashIn, i * IntPtr.Size);
                if (p == IntPtr.Zero) continue;
                var cu = Marshal.PtrToStructure<WFSCIMCASHIN>(p);
                string type = cu.fwType switch
                {
                    1 => "RECYCLING", 2 => "CASHIN", 3 => "REPCONTAINER", 4 => "RETRACTCASSETTE", 5 => "REJECTCASSETTE", 6 => "BILLCASSETTE",
                    7 => "COINCYLINDER", 8 => "COINDISPENSER", 9 => "COINHOPPER", 10 => "RETRACTPOCKET", 11 => "REJECTPOCKET", 12 => "CASHINPOCKET", _ => $"TYPE{cu.fwType}"
                };
                list.Add(new CimCashUnit(cu.usNumber, type,
                    System.Text.Encoding.ASCII.GetString(cu.cUnitID).TrimEnd('\0'),
                    (int)cu.ulCount, (int)cu.ulCashInCount, (int)cu.ulMaximum,
                    XfsConst.Name(XfsConst.CashUnitStatusNames, cu.usStatus)));
            }
            return list;
        }
        finally { XfsSession.FreeResult(r); }
    }

    /// <summary>Capabilities en crudo (lpszExtra) para el log de diagnóstico.</summary>
    public string GetCapabilitiesRaw()
    {
        var r = _s.GetInfo(XfsConst.WFS_INF_CIM_CAPABILITIES);
        try
        {
            var res = XfsSession.ReadResult(r);
            // WFSCIMCAPS: WORD wClass; WORD fwType; WORD wMaxCashInItems; BOOL bCompound; BOOL bShutter; BOOL bShutterControl;
            // BOOL bSafeDoor; BOOL bCashBox; BOOL bRefill; WORD fwIntermediateStacker; BOOL bItemsTakenSensor; BOOL bItemsInsertedSensor;
            // WORD fwPositions; WORD fwExchangeType; WORD fwRetractAreas; WORD fwRetractTransportActions; WORD fwRetractStackerActions; LPSTR lpszExtra;
            var b = res.lpBuffer;
            ushort wClass = (ushort)Marshal.ReadInt16(b, 0);
            ushort fwType = (ushort)Marshal.ReadInt16(b, 2);
            ushort maxItems = (ushort)Marshal.ReadInt16(b, 4);
            int bShutter = Marshal.ReadInt32(b, 10);
            int bShutterCtl = Marshal.ReadInt32(b, 14);
            int bSafeDoor = Marshal.ReadInt32(b, 18);
            return $"class={wClass} type={(fwType == 1 ? "TELLERBILL" : fwType == 2 ? "SELFSERVICEBILL" : fwType.ToString())} " +
                   $"maxCashInItems={maxItems} shutter={bShutter != 0} shutterControl={bShutterCtl != 0} safeDoor={bSafeDoor != 0}";
        }
        finally { XfsSession.FreeResult(r); }
    }

    // ---- Comandos de operación/técnico ----

    public void Reset(uint? timeoutMs = 120000)
    {
        // WFSCIMITEMPOSITION lpResetIn == NULL → el SP decide qué hacer con billetes remanentes
        XfsSession.FreeResult(_s.Execute(XfsConst.WFS_CMD_CIM_RESET, IntPtr.Zero, timeoutMs));
    }

    public void OpenShutter() => ShutterCmd(XfsConst.WFS_CMD_CIM_OPEN_SHUTTER);
    public void CloseShutter() => ShutterCmd(XfsConst.WFS_CMD_CIM_CLOSE_SHUTTER);

    private void ShutterCmd(uint cmd)
    {
        // LPWORD lpfwPosition; NULL → posición por defecto
        XfsSession.FreeResult(_s.Execute(cmd));
    }

    /// <summary>Inicia el depósito. Devuelve false si ya había uno activo (se continúa sobre ese).</summary>
    public bool CashInStart()
    {
        // WFSCIMCASHINSTART: USHORT usTellerID; BOOL bUseRecycleUnits; WORD fwOutputPosition; WORD fwInputPosition;
        var buf = Marshal.AllocHGlobal(10);
        try
        {
            Marshal.WriteInt16(buf, 0, 0);
            Marshal.WriteInt32(buf, 2, 0);
            Marshal.WriteInt16(buf, 6, 0);          // WFS_CIM_POSNULL
            Marshal.WriteInt16(buf, 8, 0x0004);     // WFS_CIM_POSINCENTER (la que indica SNBC; INRIGHT da UNSUPPOSITION)
            XfsSession.FreeResult(_s.Execute(XfsConst.WFS_CMD_CIM_CASH_IN_START, buf));
            return true;
        }
        catch (XfsException ex) when (ex.HResult2 == XfsConst.WFS_ERR_CIM_CASHINACTIVE)
        {
            _log.Warn("Ya había un depósito activo en el SP: se continúa sobre ese.");
            return false;
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    private Dictionary<int, (string Cur, long Val)>? _types;

    /// <summary>Tabla NoteID → moneda/valor (WFS_INF_CIM_BANKNOTE_TYPES). Se lee una vez.</summary>
    public IReadOnlyDictionary<int, (string Cur, long Val)> GetBanknoteTypes()
    {
        if (_types != null) return _types;
        var d = new Dictionary<int, (string, long)>();
        var r = _s.GetInfo(XfsConst.WFS_INF_CIM_BANKNOTE_TYPES);
        try
        {
            // WFSCIMNOTETYPELIST: USHORT usNumOfNoteTypes; LPWFSCIMNOTETYPE *lppNoteTypes;
            // WFSCIMNOTETYPE: USHORT usNoteID; CHAR cCurrencyID[3]; ULONG ulValues; USHORT usRelease; BOOL bConfigured;
            var b = XfsSession.ReadResult(r).lpBuffer;
            if (b != IntPtr.Zero)
            {
                int n = (ushort)Marshal.ReadInt16(b, 0);
                var arr = Marshal.ReadIntPtr(b, 2);
                for (int i = 0; i < n && arr != IntPtr.Zero; i++)
                {
                    var t = Marshal.ReadIntPtr(arr, i * IntPtr.Size);
                    if (t == IntPtr.Zero) continue;
                    d[(ushort)Marshal.ReadInt16(t, 0)] = (Marshal.PtrToStringAnsi(t + 2, 3) ?? "", (uint)Marshal.ReadInt32(t, 5));
                }
            }
        }
        finally { XfsSession.FreeResult(r); }
        return _types = d;
    }

    // WFSCIMNOTENUMBERLIST: USHORT usNumOfNoteNumbers; LPWFSCIMNOTENUMBER *lppNoteNumber;  WFSCIMNOTENUMBER: USHORT usNoteID; ULONG ulCount;
    private IReadOnlyList<CimNoteCount> ReadNoteList(IntPtr list)
    {
        var res = new List<CimNoteCount>();
        if (list == IntPtr.Zero) return res;
        int n = (ushort)Marshal.ReadInt16(list, 0);
        var arr = Marshal.ReadIntPtr(list, 2);
        if (arr == IntPtr.Zero) return res;
        var types = GetBanknoteTypes();
        for (int i = 0; i < n; i++)
        {
            var e = Marshal.ReadIntPtr(arr, i * IntPtr.Size);
            if (e == IntPtr.Zero) continue;
            int id = (ushort)Marshal.ReadInt16(e, 0);
            int count = Marshal.ReadInt32(e, 2);
            var t = types.TryGetValue(id, out var v) ? v : ("?", 0L);
            res.Add(new CimNoteCount(id, t.Item1, t.Item2, count));
        }
        return res;
    }

    /// <summary>Cuenta lo que haya en la boca. Devuelve los billetes aceptados en esta pasada.</summary>
    public IReadOnlyList<CimNoteCount> CashIn(uint? timeoutMs = 60000, bool quiet = false)
    {
        var r = _s.Execute(XfsConst.WFS_CMD_CIM_CASH_IN, IntPtr.Zero, timeoutMs, quiet);
        try { return ReadNoteList(XfsSession.ReadResult(r).lpBuffer); }
        finally { XfsSession.FreeResult(r); }
    }

    /// <summary>Acumulado del depósito en curso (WFS_INF_CIM_CASH_IN_STATUS): billetes aceptados y cantidad de rechazados.</summary>
    public (IReadOnlyList<CimNoteCount> Notes, int Refused) GetCashInStatus()
    {
        var r = _s.GetInfo(XfsConst.WFS_INF_CIM_CASH_IN_STATUS);
        try
        {
            // WFSCIMCASHINSTATUS: WORD wStatus; USHORT usNumOfRefused; LPWFSCIMNOTENUMBERLIST lpNoteNumberList; LPSTR lpszExtra;
            var b = XfsSession.ReadResult(r).lpBuffer;
            if (b == IntPtr.Zero) return (new List<CimNoteCount>(), 0);
            return (ReadNoteList(Marshal.ReadIntPtr(b, 4)), (ushort)Marshal.ReadInt16(b, 2));
        }
        finally { XfsSession.FreeResult(r); }
    }

    public void CashInEnd() => XfsSession.FreeResult(_s.Execute(XfsConst.WFS_CMD_CIM_CASH_IN_END, IntPtr.Zero, 120000));
    public void CashInRollback() => XfsSession.FreeResult(_s.Execute(XfsConst.WFS_CMD_CIM_CASH_IN_ROLLBACK, IntPtr.Zero, 120000));

    public void Retract()
    {
        // WFSCIMRETRACT: WORD fwOutputPosition; USHORT usRetractArea; USHORT usIndex;  usRetractArea=0x0001 RETRACT, 0x0002 TRANSPORT, 0x0004 STACKER, 0x0008 BILLCASSETTES, 0x0010 NOTSUPP, 0x0020 REJECT
        var buf = Marshal.AllocHGlobal(6);
        try
        {
            Marshal.WriteInt16(buf, 0, 0);
            Marshal.WriteInt16(buf, 2, 0x0008); // a las cash units (bolsa)
            Marshal.WriteInt16(buf, 4, 0);
            XfsSession.FreeResult(_s.Execute(XfsConst.WFS_CMD_CIM_RETRACT, buf));
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    /// <summary>Cambio de bolsa: START_EXCHANGE / END_EXCHANGE con usCount=0 (el SP resetea contadores de la unidad).</summary>
    public void StartExchange()
    {
        // WFSCIMSTARTEX: WORD fwExchangeType; USHORT usTellerID; USHORT usCount; LPUSHORT lpusCUNumList; LPWFSCIMOUTPUT lpOutput;
        var buf = Marshal.AllocHGlobal(6 + IntPtr.Size * 2);
        try
        {
            Marshal.WriteInt16(buf, 0, 0x0001); // WFS_CIM_EXBYHAND
            Marshal.WriteInt16(buf, 2, 0);
            Marshal.WriteInt16(buf, 4, 0);
            Marshal.WriteIntPtr(buf, 6, IntPtr.Zero);
            Marshal.WriteIntPtr(buf, 6 + IntPtr.Size, IntPtr.Zero);
            XfsSession.FreeResult(_s.Execute(XfsConst.WFS_CMD_CIM_START_EXCHANGE, buf));
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    public void EndExchange() => XfsSession.FreeResult(_s.Execute(XfsConst.WFS_CMD_CIM_END_EXCHANGE));

    private static string ReadMultiSz(IntPtr p)
    {
        var parts = new List<string>();
        int offset = 0;
        while (true)
        {
            string s = Marshal.PtrToStringAnsi(p + offset) ?? "";
            if (s.Length == 0) break;
            parts.Add(s);
            offset += s.Length + 1;
        }
        return string.Join(" | ", parts);
    }

    public void Dispose() => _s.Dispose();
}
