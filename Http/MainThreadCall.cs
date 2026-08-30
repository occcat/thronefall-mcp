using System;

namespace ThronefallControl.Http;

public static class MainThreadCall
{
    public static HttpResponse Invoke(Func<HttpResponse> work) => MutateHttp.OnMainThread(work);
}
