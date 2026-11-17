**Para:** Pablo / Soren (SNBC)
**Asunto:** BDM-500 Galicia – supervisor app: XFS VDM, SP logical names and BNE tool

Hi Pablo, Soren,

Galicia asked us for a local supervisor / diagnostic application for the BDM-500 (same core as the BDM-100 they already run with Softy over XFS). We are building it in-house on top of your XFS Service Provider, so I need three things from your engineering team:

1. **XFS SP documentation** for the BDM-500: logical service names registered under `HKLM\SOFTWARE\XFS\LOGICAL_SERVICES`, supported CIM commands/events, and whether the SP implements the **VDM (Vendor Dependent Mode) service class** (`WFS_CMD_VDM_ENTER_MODE_REQ` / `EXIT_MODE_REQ`). If it does not, please confirm the recommended way for a maintenance application to take over the device while the bank application is running.

2. **BNE-S110M (A110M) PC tool**: the Windows utility used for firmware update, banknote template loading and sensor calibration — installer, user guide, and whether it can be launched from the command line (so our app can call it).

3. Confirmation that the SP's `WFS_INF_CIM_STATUS` / `WFS_INF_CIM_CASH_UNIT_INFO` structures follow standard CIM 3.x, and what you return in `lpszExtra` (firmware version, template version, etc.).

If you already have a VDA / supervisor application of your own for the BDM series, please send it as well; we may integrate it instead of rebuilding the low-level parts.

Timing matters: Galicia wants to see this during the branch pilot of the BDM-500 in the coming weeks.

Thanks,
Diego Cores
Feedbill
