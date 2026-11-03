using System.Runtime.InteropServices;
using BdmSupervisor.Services;

namespace BdmSupervisor.Xfs;

/// <summary>
/// Servicio VDM (Vendor Dependent Mode) de XFS, con el protocolo completo:
///   1. WFSRegister(SERVICE_EVENTS) en una ventana nuestra.
///   2. WFSAsyncExecute(ENTER_MODE_REQ).
///   3. El SP emite WFS_SRVE_VDM_ENTER_MODE_REQ a todas las apps registradas (Softy y nosotros);
///      cada una libera el hardware y responde ENTER_MODE_ACK.
///   4. Cuando todas respondieron, el SP emite WFS_SRVE_VDM_MODEENTERED → modo activo.
/// Salida: igual con EXIT_MODE_REQ / EXIT_MODE_ACK / MODEEXITED.
/// Las llamadas Enter/Exit deben hacerse desde el hilo de la UI (el que bombea los mensajes de la ventana).
/// </summary>
public sealed class VdmService : IDisposable
{
    private readonly XfsSession _s;
    private readonly LogService _log;
    private IXfsEventSource? _ev;
    private TaskCompletionSource<bool>? _tcs;
    private uint _reqCmd, _ackCmd, _doneEvent, _reqId;

    public VdmService(XfsSession session, LogService log) { _s = session; _log = log; }

    public bool IsAttached => _ev is not null;

    public void Attach(IXfsEventSource ev)
    {
        _ev = ev;
        _s.Register(ev.Handle, XfsConst.SERVICE_EVENTS | XfsConst.SYSTEM_EVENTS | XfsConst.EXECUTE_EVENTS);
        ev.Message += OnMessage;
        _log.Info($"VDM: registrado a eventos (hService={_s.HService})");
    }

    // WFSVDMSTATUS: WORD wDevice; WORD wService; ...
    public string GetStatus()
    {
        var r = _s.GetInfo(XfsConst.WFS_INF_VDM_STATUS);
        try
        {
            var res = XfsSession.ReadResult(r);
            ushort wDevice = (ushort)Marshal.ReadInt16(res.lpBuffer, 0);
            ushort wService = (ushort)Marshal.ReadInt16(res.lpBuffer, 2);
            return XfsConst.Name(XfsConst.VdmServiceNames, wService) + (wDevice == 0 ? "" : $" (device={wDevice})");
        }
        finally { XfsSession.FreeResult(r); }
    }

    public Task EnterModeAsync(int timeoutMs = 60000) =>
        Request(XfsConst.WFS_CMD_VDM_ENTER_MODE_REQ, XfsConst.WFS_CMD_VDM_ENTER_MODE_ACK, XfsConst.WFS_SRVE_VDM_MODEENTERED, timeoutMs);

    public Task ExitModeAsync(int timeoutMs = 60000) =>
        Request(XfsConst.WFS_CMD_VDM_EXIT_MODE_REQ, XfsConst.WFS_CMD_VDM_EXIT_MODE_ACK, XfsConst.WFS_SRVE_VDM_MODEEXITED, timeoutMs);

    private async Task Request(uint reqCmd, uint ackCmd, uint doneEvent, int timeoutMs)
    {
        if (_ev is null) throw new InvalidOperationException("VDM sin ventana de eventos (Attach no llamado)");
        if (_tcs is { Task.IsCompleted: false }) throw new InvalidOperationException("Ya hay un pedido VDM en curso");
        _reqCmd = reqCmd; _ackCmd = ackCmd; _doneEvent = doneEvent;
        _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _log.Info($"VDM → {CmdName(reqCmd)}");
        int hr = XfsNative.WFSAsyncExecute(_s.HService, reqCmd, IntPtr.Zero, (uint)timeoutMs, _ev.Handle, out _reqId);
        if (hr != XfsConst.WFS_SUCCESS) throw new XfsException(hr, $"WFSAsyncExecute({CmdName(reqCmd)})");

        // ACK proactivo: el SP espera ENTER/EXIT_MODE_ACK de cada app registrada (nosotros incluidos).
        // Lo mandamos sin esperar el evento, por si la entrega del evento a nuestra ventana falla.
        await Task.Delay(500);
        if (!_tcs.Task.IsCompleted)
        {
            int hrAck = XfsNative.WFSAsyncExecute(_s.HService, ackCmd, IntPtr.Zero, 10000, _ev.Handle, out uint ackId);
            _log.Info($"VDM ← {CmdName(ackCmd)} proactivo (req {ackId}): {XfsConst.ErrorName(hrAck)}");
        }

        var done = await Task.WhenAny(_tcs.Task, Task.Delay(timeoutMs + 2000));
        if (done != _tcs.Task) throw new TimeoutException($"VDM: sin respuesta del SP a {CmdName(reqCmd)} en {timeoutMs / 1000}s");
        await _tcs.Task; // propaga excepción si la hubo
        _log.Info($"VDM: {EvName(doneEvent)}");
    }

    private void OnMessage(int msg, XfsNative.WFSRESULT r)
    {
        _log.Info($"XFS msg 0x{msg:X4} hService={r.hService} code={r.dwCommandCode} hResult={r.hResult} req={r.RequestID}");
        if (r.hService != _s.HService && r.hService != 0) return;
        switch (msg)
        {
            case XfsNative.WFS_SERVICE_EVENT:
                uint ev = r.dwCommandCode;
                _log.Info($"VDM evento {EvName(ev)}");
                if (ev == XfsConst.WFS_SRVE_VDM_ENTER_MODE_REQ || ev == XfsConst.WFS_SRVE_VDM_EXIT_MODE_REQ)
                {
                    // Nos toca liberar/retomar el hardware (no tenemos nada abierto en exclusiva) y confirmar
                    uint ack = ev == XfsConst.WFS_SRVE_VDM_ENTER_MODE_REQ ? XfsConst.WFS_CMD_VDM_ENTER_MODE_ACK : XfsConst.WFS_CMD_VDM_EXIT_MODE_ACK;
                    int hr = XfsNative.WFSAsyncExecute(_s.HService, ack, IntPtr.Zero, 10000, _ev!.Handle, out _);
                    _log.Info($"VDM ← {CmdName(ack)} ({XfsConst.ErrorName(hr)})");
                }
                else if (ev == _doneEvent)
                {
                    _tcs?.TrySetResult(true);
                }
                break;

            case XfsNative.WFS_EXECUTE_COMPLETE:
                if (r.RequestID == _reqId)
                {
                    if (r.hResult != XfsConst.WFS_SUCCESS)
                        _tcs?.TrySetException(new XfsException(r.hResult, CmdName(_reqCmd)));
                    else
                    {
                        _log.Info($"VDM {CmdName(_reqCmd)} completado por el SP");
                        _tcs?.TrySetResult(true); // algunos SP completan el REQ recién cuando el modo cambió
                    }
                }
                else if (r.hResult != XfsConst.WFS_SUCCESS)
                    _log.Warn($"VDM {CmdName(r.dwCommandCode)} → {XfsConst.ErrorName(r.hResult)}");
                break;

            case XfsNative.WFS_SYSTEM_EVENT:
                _log.Warn($"VDM evento de sistema {r.dwCommandCode} hResult={r.hResult}");
                break;
        }
    }

    private static string CmdName(uint c) => c switch
    {
        XfsConst.WFS_CMD_VDM_ENTER_MODE_REQ => "ENTER_MODE_REQ", XfsConst.WFS_CMD_VDM_ENTER_MODE_ACK => "ENTER_MODE_ACK",
        XfsConst.WFS_CMD_VDM_EXIT_MODE_REQ => "EXIT_MODE_REQ", XfsConst.WFS_CMD_VDM_EXIT_MODE_ACK => "EXIT_MODE_ACK", _ => $"cmd {c}"
    };
    private static string EvName(uint e) => e switch
    {
        XfsConst.WFS_SRVE_VDM_ENTER_MODE_REQ => "ENTER_MODE_REQ", XfsConst.WFS_SRVE_VDM_EXIT_MODE_REQ => "EXIT_MODE_REQ",
        XfsConst.WFS_SRVE_VDM_MODEENTERED => "MODEENTERED", XfsConst.WFS_SRVE_VDM_MODEEXITED => "MODEEXITED", _ => $"evento {e}"
    };

    public void Dispose()
    {
        if (_ev is not null) _ev.Message -= OnMessage;
        _s.Dispose();
    }
}
