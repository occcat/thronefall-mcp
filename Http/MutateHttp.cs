using System;
using System.Threading;
using ThronefallControl.Dto;
using ThronefallControl.Game;

namespace ThronefallControl.Http;

public static class MutateHttp
{
    public const string TimeoutMessage = "main thread timed out";

    public static HttpResponse OnMainThread(Func<HttpResponse> work)
    {
        var snap = new Snapshot();
        try
        {
            HttpResponse Wrapped()
            {
                var game = GameFacade.Current;
                snap.Phase = game.World.Phase;
                snap.Generation = game.Ids.SceneGeneration;
                Volatile.Write(ref snap.Ready, 1);
                return work();
            }

            var mt = MainThread.Current;
            return mt == null ? Wrapped() : mt.Run(Wrapped).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            return FromCaught(ex, snap);
        }
    }

    public static HttpResponse FromCaught(Exception ex) => FromCaught(ex, null);

    public static bool IsJsonParseError(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException!)
        {
            var name = e.GetType().Name;
            if (name.IndexOf("Json", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    public static HttpResponse InvalidJson(Exception ex) =>
        Json.Error(400, "invalid_json", "request body is not valid JSON: " + ex.Message);

    static HttpResponse FromCaught(Exception ex, Snapshot? snap)
    {
        var inner = Unwrap(ex);
        ReadSnap(snap, out var phase, out var generation);
        if (inner is MainThreadTimeoutException)
            return Json.Error(504, ErrorCodes.MainThreadTimeout, TimeoutMessage, phase, generation);

        return Json.Error(500, ErrorCodes.UnityException, inner.GetBaseException().Message, phase, generation);
    }

    static Exception Unwrap(Exception ex)
    {
        while (ex is AggregateException ag && ag.InnerException != null)
            ex = ag.InnerException;
        return ex;
    }

    static void ReadSnap(Snapshot? snap, out string? phase, out int? generation)
    {
        if (snap != null && Volatile.Read(ref snap.Ready) == 1)
        {
            phase = snap.Phase;
            generation = snap.Generation;
            return;
        }

        phase = null;
        generation = null;
    }

    sealed class Snapshot
    {
        public string? Phase;
        public int Generation;
        public int Ready;
    }
}
