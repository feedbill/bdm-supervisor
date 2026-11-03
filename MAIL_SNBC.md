**To:** Pablo / Soren (SNBC)
**Subject:** BDM-500 Galicia – supervisor app integration: VDM, SiuIsControl and fwSafeDoor

Hi Pablo, Soren,

We built a supervisor/diagnostic application for the BDM-500 on top of your XFS SP (SP 2.0.1.2 / SnbcTCR 2.0.3.1 / BNE SDK 2.7.28.9) and have it running with a BNE-S110M. Three points where we need your confirmation:

1. **VDM / TestTool.** We register our app as `VDM_SNBC\Config\TestTool` so the SP launches it when entering vendor mode. The SP fires `WFS_SRVE_VDM_ENTER_MODE_REQ` but our registered window never receives the event (we do receive `WFS_EXECUTE_COMPLETE`), so we send `ENTER_MODE_ACK` right after the request, which works. Is this the intended flow, and does Softy need to send its own ACK?

2. **No electric shutter (Galicia configuration).** Following your note we set `SiuIsControl = 0` and `BoardCount = 1`; the device goes ONLINE and the `SafeShutter status error` disappears. Please confirm that `ShutterDropDoorSurport = 0` in `CRM_TCR.ini` is also the correct setting for a BDM-500 without the drop-door shutter.

3. **fwSafeDoor.** With `SaveDoorSurport = 0` the SP reports `fwSafeDoor = 1` (your log labels it DOORNOTSUPPORTED, but in CEN/XFS CIM the value 1 means DOOROPEN; NOTSUPPORTED is 0). How does Softy at Galicia interpret this value on a BDM-500 without SIU board? Should the SP return 0 instead?

Also, if you have a PC tool for BNE-S110M firmware/template updates that can be launched from the command line, please send it so we can integrate it in the maintenance screen.

Thanks,
Diego Cores
Feedbill
