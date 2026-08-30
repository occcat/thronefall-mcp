using System;
using ThronefallControl.Dto;
using ThronefallControl.Game;

namespace ThronefallControl.Http;

public static class MainThreadCall
{
    public static HttpResponse Invoke(Func<HttpResponse> work)
    {
        var mt = MainThread.Current;
        if (mt == null)
            return work();

        try
        {
            return mt.Run(work).GetAwaiter().GetResult();
        }
        catch (MainThreadTimeoutException)
        {
            return PhaseGate.Fail(504, ErrorCodes.MainThreadTimeout, "main thread timeout");
        }
        catch (Exception ex)
        {
            return PhaseGate.Fail(500, ErrorCodes.UnityException, ex.Message);
        }
    }
}