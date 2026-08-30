using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using ThronefallControl.Config;
using ThronefallControl.Dto;
using ThronefallControl.Game;

namespace ThronefallControl.Http;

public sealed class Server : IDisposable
{
    readonly Router _router;
    readonly Action<string>? _logInfo;
    readonly Action<string>? _logError;

    NativeHttpListener? _listener;
    Thread? _thread;
    volatile bool _stopped;

    public Server(
        Router? router = null,
        Action<string>? logInfo = null,
        Action<string>? logError = null)
    {
        _router = router ?? Router.CreateDefault();
        _logInfo = logInfo;
        _logError = logError;
    }

    public bool IsListening { get; private set; }

    public Router Router => _router;

    public HttpResponse Process(RequestContext ctx)
    {
        try
        {
            if (!Auth.TryAuthorize(ctx, out var error))
                return error!;
            return _router.Dispatch(ctx);
        }
        catch (MainThreadTimeoutException ex)
        {
            return Json.Error(504, ErrorCodes.MainThreadTimeout, ex.Message);
        }
        catch (Exception ex)
        {
            var inner = ex.InnerException ?? ex;
            if (inner is MainThreadTimeoutException)
                return Json.Error(504, ErrorCodes.MainThreadTimeout, inner.Message);
            return Json.Error(500, ErrorCodes.UnityException, inner.Message);
        }
    }

    public void Start()
    {
        Stop();
        _stopped = false;
        IsListening = false;

        var address = PluginConfig.BindAddress;
        var port = PluginConfig.HttpPort;
        try
        {
            if (!IsLoopback(address))
            {
                _logError?.Invoke(
                    $"refusing non-loopback HTTP bind {address}:{port}; plugin continues without API");
                return;
            }

            if (port <= 0 || port > 65535)
            {
                _logError?.Invoke($"invalid HTTP port {port}; plugin continues without API");
                return;
            }

            if (!NativeHttpListener.Available)
            {
                _logError?.Invoke("HttpListener is not available; plugin continues without API");
                return;
            }

            var prefix = BuildPrefix(address, port);
            var listener = new NativeHttpListener();
            listener.IgnoreWriteExceptions();
            listener.AddPrefix(prefix);
            listener.Start();
            _listener = listener;
            _thread = new Thread(ListenLoop)
            {
                IsBackground = true,
                Name = "ThronefallHttp"
            };
            _thread.Start();
            IsListening = true;
            _logInfo?.Invoke($"HTTP listening on {prefix}");
        }
        catch (Exception ex)
        {
            IsListening = false;
            _logError?.Invoke($"HTTP bind failed on {address}:{port}: {Unwrap(ex)}");
            TryCloseListener();
        }
    }

    public void Stop()
    {
        _stopped = true;
        IsListening = false;
        TryCloseListener();
        var thread = _thread;
        _thread = null;
        if (thread != null && thread != Thread.CurrentThread)
            thread.Join(TimeSpan.FromSeconds(2));
    }

    public void Dispose() => Stop();

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

    public static string BuildPrefix(string address, int port)
    {
        var host = (address ?? "").Trim();
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
            host = "127.0.0.1";
        if (IPAddress.TryParse(host, out var ip) && ip.AddressFamily == AddressFamily.InterNetworkV6)
            host = $"[{ip}]";
        return $"http://{host}:{port}/";
    }

    void ListenLoop()
    {
        while (!_stopped)
        {
            object context;
            try
            {
                var listener = _listener;
                if (listener == null)
                    break;
                context = listener.GetContext();
            }
            catch (Exception ex)
            {
                if (_stopped)
                    break;
                _logError?.Invoke($"HTTP GetContext failed: {Unwrap(ex)}");
                continue;
            }

            ThreadPool.QueueUserWorkItem(_ => Serve(context));
        }
    }

    void Serve(object context)
    {
        try
        {
            var request = MapRequest(context);
            var response = Process(request);
            WriteResponse(context, response);
        }
        catch (Exception ex)
        {
            try
            {
                WriteResponse(context, Json.Error(500, ErrorCodes.UnityException, Unwrap(ex)));
            }
            catch
            {
                // Client closed the connection.
            }
        }
    }

    void TryCloseListener()
    {
        var listener = _listener;
        _listener = null;
        if (listener == null)
            return;
        try { listener.Stop(); } catch { /* already closed */ }
        try { listener.Abort(); } catch { /* already closed */ }
        try { listener.Close(); } catch { /* already closed */ }
    }

    static RequestContext MapRequest(object context)
    {
        var req = Prop(context, "Request")!;
        var method = (string)(Prop(req, "HttpMethod") ?? "GET");
        var rawUrl = (string?)Prop(req, "RawUrl") ?? "/";
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (Prop(req, "Headers") is NameValueCollection nvc)
        {
            foreach (var key in nvc.AllKeys)
            {
                if (key == null)
                    continue;
                headers[key] = nvc[key] ?? "";
            }
        }

        var body = "";
        if (Prop(req, "InputStream") is Stream stream)
        {
            var enc = Prop(req, "ContentEncoding") as Encoding ?? Encoding.UTF8;
            using var reader = new StreamReader(stream, enc, true, 1024, true);
            body = reader.ReadToEnd();
        }

        return RequestContext.Create(method, rawUrl, headers, body);
    }

    static void WriteResponse(object context, HttpResponse response)
    {
        var res = Prop(context, "Response")!;
        var bytes = Encoding.UTF8.GetBytes(response.Body ?? "");
        SetProp(res, "StatusCode", response.Status);
        SetProp(res, "ContentType", response.ContentType);
        SetProp(res, "ContentLength64", (long)bytes.Length);
        SetProp(res, "SendChunked", false);
        if (Prop(res, "OutputStream") is Stream output)
        {
            output.Write(bytes, 0, bytes.Length);
            output.Flush();
        }

        res.GetType().GetMethod("Close", Type.EmptyTypes)?.Invoke(res, null);
    }

    static object? Prop(object target, string name) =>
        target.GetType().GetProperty(name)?.GetValue(target);

    static void SetProp(object target, string name, object? value)
    {
        var prop = target.GetType().GetProperty(name);
        if (prop == null || !prop.CanWrite)
            return;
        prop.SetValue(target, value);
    }

    static string Unwrap(Exception ex)
    {
        var inner = ex is TargetInvocationException tie ? tie.InnerException ?? ex : ex;
        return inner.Message;
    }

    sealed class NativeHttpListener
    {
        static readonly Type? ListenerType = Resolve();
        readonly object _instance;
        readonly MethodInfo _start;
        readonly MethodInfo _stop;
        readonly MethodInfo _close;
        readonly MethodInfo? _abort;
        readonly MethodInfo _getContext;
        readonly PropertyInfo _prefixes;
        readonly PropertyInfo? _ignoreWriteExceptions;

        public static bool Available => ListenerType != null;

        public NativeHttpListener()
        {
            if (ListenerType == null)
                throw new NotSupportedException("HttpListener type not found");
            _instance = Activator.CreateInstance(ListenerType)
                ?? throw new NotSupportedException("HttpListener could not be constructed");
            _start = RequireMethod("Start");
            _stop = RequireMethod("Stop");
            _close = RequireMethod("Close");
            _abort = ListenerType.GetMethod("Abort", Type.EmptyTypes);
            _getContext = RequireMethod("GetContext");
            _prefixes = ListenerType.GetProperty("Prefixes")
                ?? throw new NotSupportedException("HttpListener.Prefixes missing");
            _ignoreWriteExceptions = ListenerType.GetProperty("IgnoreWriteExceptions");
        }

        public void AddPrefix(string prefix)
        {
            var prefixes = _prefixes.GetValue(_instance)
                ?? throw new InvalidOperationException("HttpListener.Prefixes is null");
            var add = prefixes.GetType().GetMethod("Add", new[] { typeof(string) })
                ?? throw new NotSupportedException("HttpListenerPrefixCollection.Add missing");
            add.Invoke(prefixes, new object[] { prefix });
        }

        public void IgnoreWriteExceptions()
        {
            if (_ignoreWriteExceptions == null || !_ignoreWriteExceptions.CanWrite)
                return;
            _ignoreWriteExceptions.SetValue(_instance, true);
        }

        public void Start() => _start.Invoke(_instance, null);

        public void Stop() => _stop.Invoke(_instance, null);

        public void Abort() => _abort?.Invoke(_instance, null);

        public void Close() => _close.Invoke(_instance, null);

        public object GetContext() =>
            _getContext.Invoke(_instance, null)
            ?? throw new InvalidOperationException("HttpListener.GetContext returned null");

        static MethodInfo RequireMethod(string name) =>
            ListenerType!.GetMethod(name, Type.EmptyTypes)
            ?? throw new NotSupportedException($"HttpListener.{name} missing");

        static Type? Resolve()
        {
#if NETSTANDARD2_1
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType("System.Net.HttpListener");
                if (t != null)
                    return t;
            }

            try
            {
                var loaded = Assembly.Load("System");
                var t = loaded.GetType("System.Net.HttpListener");
                if (t != null)
                    return t;
            }
            catch
            {
                // System.dll is present in Unity Mono but not in the netstandard reference set.
            }

            return Type.GetType("System.Net.HttpListener, System")
                ?? Type.GetType("System.Net.HttpListener");
#else
            return typeof(HttpListener);
#endif
        }
    }
}
