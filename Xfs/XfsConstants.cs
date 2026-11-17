namespace BdmSupervisor.Xfs;

/// <summary>Constantes CEN/XFS 3.x relevantes para CIM (Cash-In Module) y VDM (Vendor Dependent Mode).</summary>
public static class XfsConst
{
    // ---- Resultados generales (CEN/XFS 3.x) ----
    public const int WFS_SUCCESS = 0;
    public const int WFS_ERR_ALREADY_STARTED = -1;
    public const int WFS_ERR_API_VER_TOO_HIGH = -2;
    public const int WFS_ERR_API_VER_TOO_LOW = -3;
    public const int WFS_ERR_CANCELED = -4;
    public const int WFS_ERR_DEV_NOT_READY = -13;
    public const int WFS_ERR_HARDWARE_ERROR = -14;
    public const int WFS_ERR_INTERNAL_ERROR = -15;
    public const int WFS_ERR_INVALID_HSERVICE = -22;
    public const int WFS_ERR_LOCKED = -32;
    public const int WFS_ERR_NOT_STARTED = -39;
    public const int WFS_ERR_SERVICE_NOT_FOUND = -43;
    public const int WFS_ERR_SPI_VER_TOO_HIGH = -44;
    public const int WFS_ERR_SPI_VER_TOO_LOW = -45;
    public const int WFS_ERR_SRVC_VER_TOO_HIGH = -46;
    public const int WFS_ERR_SRVC_VER_TOO_LOW = -47;
    public const int WFS_ERR_TIMEOUT = -48;
    public const int WFS_ERR_UNSUPP_CATEGORY = -49;
    public const int WFS_ERR_UNSUPP_COMMAND = -50;
    public const int WFS_ERR_VERSION_ERROR_IN_SRVC = -51;
    public const int WFS_ERR_INVALID_DATA = -52;
    public const int WFS_ERR_SOFTWARE_ERROR = -53;
    public const int WFS_ERR_CONNECTION_LOST = -54;
    public const int WFS_ERR_USER_ERROR = -55;
    public const int WFS_ERR_UNSUPP_DATA = -56;
    public const int WFS_ERR_FRAUD_ATTEMPT = -57;
    public const int WFS_ERR_SEQUENCE_ERROR = -58;
    public const int WFS_ERR_AUTH_REQUIRED = -59;

    // ---- Errores CIM usados por la app (verificados contra el SP de SNBC) ----
    public const int WFS_ERR_CIM_NOITEMS = -1316;
    public const int WFS_ERR_CIM_CASHINACTIVE = -1325;
    public const int WFS_ERR_CIM_NOCASHINACTIVE = -1326;

    private static readonly string[] ErrNames =
    [
        "WFS_SUCCESS","ALREADY_STARTED","API_VER_TOO_HIGH","API_VER_TOO_LOW","CANCELED","CFG_INVALID_HKEY","CFG_INVALID_NAME",
        "CFG_INVALID_SUBKEY","CFG_INVALID_VALUE","CFG_KEY_NOT_EMPTY","CFG_NAME_TOO_LONG","CFG_NO_MORE_ITEMS","CFG_VALUE_TOO_LONG",
        "DEV_NOT_READY","HARDWARE_ERROR","INTERNAL_ERROR","INVALID_ADDRESS","INVALID_APP_HANDLE","INVALID_BUFFER","INVALID_CATEGORY",
        "INVALID_COMMAND","INVALID_EVENT_CLASS","INVALID_HSERVICE","INVALID_HPROVIDER","INVALID_HWND","INVALID_HWNDREG","INVALID_POINTER",
        "INVALID_REQ_ID","INVALID_RESULT","INVALID_SERVPROV","INVALID_TIMER","INVALID_TRACELEVEL","LOCKED","NO_BLOCKING_CALL",
        "NO_SERVPROV","NO_SUCH_THREAD","NO_TIMER","NOT_LOCKED","NOT_OK_TO_UNLOAD","NOT_STARTED","NOT_REGISTERED","OP_IN_PROGRESS",
        "OUT_OF_MEMORY","SERVICE_NOT_FOUND","SPI_VER_TOO_HIGH","SPI_VER_TOO_LOW","SRVC_VER_TOO_HIGH","SRVC_VER_TOO_LOW","TIMEOUT",
        "UNSUPP_CATEGORY","UNSUPP_COMMAND","VERSION_ERROR_IN_SRVC","INVALID_DATA","SOFTWARE_ERROR","CONNECTION_LOST","USER_ERROR",
        "UNSUPP_DATA","FRAUD_ATTEMPT","SEQUENCE_ERROR","AUTH_REQUIRED"
    ];

    // ---- Clases de servicio ----
    public const uint WFS_SERVICE_CLASS_VDM = 9;
    public const uint WFS_SERVICE_CLASS_CIM = 13;

    // ---- Clases de eventos (WFSRegister) ----
    public const uint SERVICE_EVENTS = 1;
    public const uint USER_EVENTS = 2;
    public const uint SYSTEM_EVENTS = 4;
    public const uint EXECUTE_EVENTS = 8;

    // ---- CIM: offset 1300 ----
    public const uint WFS_INF_CIM_STATUS = 1301;
    public const uint WFS_INF_CIM_CAPABILITIES = 1302;
    public const uint WFS_INF_CIM_CASH_UNIT_INFO = 1303;
    public const uint WFS_INF_CIM_BANKNOTE_TYPES = 1306;
    public const uint WFS_INF_CIM_CASH_IN_STATUS = 1307;

    public const uint WFS_CMD_CIM_CASH_IN_START = 1301;
    public const uint WFS_CMD_CIM_CASH_IN = 1302;
    public const uint WFS_CMD_CIM_CASH_IN_END = 1303;
    public const uint WFS_CMD_CIM_CASH_IN_ROLLBACK = 1304;
    public const uint WFS_CMD_CIM_RETRACT = 1305;
    public const uint WFS_CMD_CIM_OPEN_SHUTTER = 1306;
    public const uint WFS_CMD_CIM_CLOSE_SHUTTER = 1307;
    public const uint WFS_CMD_CIM_SET_CASH_UNIT_INFO = 1309;
    public const uint WFS_CMD_CIM_START_EXCHANGE = 1310;
    public const uint WFS_CMD_CIM_END_EXCHANGE = 1311;
    public const uint WFS_CMD_CIM_RESET = 1313;

    // fwDevice
    public static readonly string[] DeviceNames =
        ["ONLINE", "OFFLINE", "POWEROFF", "NODEVICE", "HWERROR", "USERERROR", "BUSY", "FRAUDATTEMPT", "POTENTIALFRAUD"];
    // fwSafeDoor
    public static readonly string[] SafeDoorNames = ["NOTSUPP", "OPEN", "CLOSED", "UNKNOWN"];
    // fwAcceptor
    public static readonly string[] AcceptorNames = ["OK", "CUSTATE", "CUSTOP", "CUUNKNOWN"];
    // fwIntermediateStacker
    public static readonly string[] StackerNames = ["EMPTY", "NOTEMPTY", "FULL", "UNKNOWN", "NOTSUPP"];
    // fwBanknoteReader
    public static readonly string[] BnrNames = ["OK", "INOP", "UNKNOWN", "NOTSUPP"];
    // fwShutter (WFSCIMINPOS)
    public static readonly string[] ShutterNames = ["CLOSED", "OPEN", "JAMMED", "UNKNOWN", "NOTSUPP"];
    // fwPositionStatus
    public static readonly string[] PosStatusNames = ["EMPTY", "NOTEMPTY", "UNKNOWN", "NOTSUPP"];
    // fwTransport
    public static readonly string[] TransportNames = ["OK", "INOP", "UNKNOWN", "NOTSUPP"];
    // usStatus de cash unit
    public static readonly string[] CashUnitStatusNames =
        ["OK", "FULL", "HIGH", "LOW", "EMPTY", "INOP", "MISSING", "NOVAL", "NOREF", "MANIP"];

    // ---- VDM: offset 900 ----
    public const uint WFS_INF_VDM_STATUS = 901;
    public const uint WFS_INF_VDM_CAPABILITIES = 902;

    public const uint WFS_CMD_VDM_ENTER_MODE_REQ = 901;
    public const uint WFS_CMD_VDM_ENTER_MODE_ACK = 902;
    public const uint WFS_CMD_VDM_EXIT_MODE_REQ = 903;
    public const uint WFS_CMD_VDM_EXIT_MODE_ACK = 904;

    public const uint WFS_SRVE_VDM_ENTER_MODE_REQ = 901;
    public const uint WFS_SRVE_VDM_EXIT_MODE_REQ = 902;
    public const uint WFS_SRVE_VDM_MODEENTERED = 903;
    public const uint WFS_SRVE_VDM_MODEEXITED = 904;

    // wService de WFSVDMSTATUS
    public static readonly string[] VdmServiceNames = ["ENTER_PENDING", "ACTIVE", "EXIT_PENDING", "INACTIVE"];

    public static string Name(string[] table, int value) =>
        value >= 0 && value < table.Length ? table[value] : $"?({value})";

    public static string ErrorName(int hr)
    {
        int idx = -hr;
        if (idx >= 0 && idx < ErrNames.Length) return idx == 0 ? ErrNames[0] : "WFS_ERR_" + ErrNames[idx];
        // Errores específicos de clase (CIM: -1300.., VDM: -900..)
        return hr switch
        {
            -1300 => "WFS_ERR_CIM_INVALIDCURRENCY", -1301 => "WFS_ERR_CIM_INVALIDTELLERID", -1302 => "WFS_ERR_CIM_CASHUNITERROR",
            -1307 => "WFS_ERR_CIM_TOOMANYITEMS", -1308 => "WFS_ERR_CIM_UNSUPPOSITION", -1310 => "WFS_ERR_CIM_SAFEDOOROPEN",
            -1312 => "WFS_ERR_CIM_SHUTTERNOTOPEN", -1313 => "WFS_ERR_CIM_SHUTTEROPEN", -1314 => "WFS_ERR_CIM_SHUTTERCLOSED",
            -1315 => "WFS_ERR_CIM_INVALIDCASHUNIT", -1316 => "WFS_ERR_CIM_NOITEMS", -1317 => "WFS_ERR_CIM_EXCHANGEACTIVE",
            -1318 => "WFS_ERR_CIM_NOEXCHANGEACTIVE", -1319 => "WFS_ERR_CIM_SHUTTERNOTCLOSED", -1323 => "WFS_ERR_CIM_ITEMSTAKEN",
            -1325 => "WFS_ERR_CIM_CASHINACTIVE", -1326 => "WFS_ERR_CIM_NOCASHINACTIVE", -1328 => "WFS_ERR_CIM_POSITION_NOT_EMPTY",
            -1334 => "WFS_ERR_CIM_INVALIDRETRACTPOSITION", -1335 => "WFS_ERR_CIM_NOTRETRACTAREA",
            _ => $"XFS error {hr}"
        };
    }
}
