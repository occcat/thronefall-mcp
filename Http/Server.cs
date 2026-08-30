using System;
using System.Net;
using ThronefallControl.Config;

namespace ThronefallControl.Http;

public sealed class Server
{
    readonly Router _router;

    public Server(Router? router = null)
    {
        _router = router ?? Router.CreateDefault();
    }

    public bool IsListening { get; private set; }

    public Router Router => _router;

    public HttpResponse Process(RequestContext ctx)
    {
        if (!Auth.TryAuthorize(ctx, out var error))
            return error!;
        return _router.Dispatch(ctx);
    }

    public void Start()
    {
        try
        {
            if (!IsLoopback(PluginConfig.BindAddress))
            {
                IsListening = false;
                return;
            }

            // HttpListener is not in the netstandard2.1 reference assemblies.
            // The HTTP worker binds here; bind failure must not throw out of Awake.
            IsListening = false;
        }
        catch
        {
            IsListening = false;
        }
    }

    public void Stop()
    {
        IsListening = false;
    }

    public static bool IsLoopback(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return false;
        switch (address.Trim())
        {
            case "*":
            case "+":
            case "0.0.0.0":
            case "::":
            case "[::]":
                return false;
            case "localhost":
                return true;
        }

        return IPAddress.TryParse(address, out var ip) && IPAddress.IsLoopback(ip);
    }
}
