using System;
using System.Collections.Generic;
using System.Linq;
using ThronefallControl.Dto;

namespace ThronefallControl.Http;

public sealed class Router
{
    readonly List<Route> _routes = new();

    public void Map(string method, string pattern, Func<RequestContext, HttpResponse> handler)
    {
        _routes.Add(new Route(method.ToUpperInvariant(), Split(pattern), handler));
    }

    public void AddModule(IRouteModule module) => module.Register(this);

    public static Router CreateDefault()
    {
        var router = new Router();
        foreach (var module in DiscoverModules())
            router.AddModule(module);
        return router;
    }

    public static IReadOnlyList<IRouteModule> DiscoverModules()
    {
        var list = new List<IRouteModule>();
        foreach (var type in typeof(IRouteModule).Assembly.GetTypes())
        {
            if (!type.IsClass || type.IsAbstract || type.IsNested)
                continue;
            if (!type.IsPublic || !typeof(IRouteModule).IsAssignableFrom(type))
                continue;
            if (type.GetConstructor(Type.EmptyTypes) == null)
                continue;
            if (Activator.CreateInstance(type) is IRouteModule module)
                list.Add(module);
        }

        return list.OrderBy(m => m.GetType().FullName, StringComparer.Ordinal).ToArray();
    }

    public HttpResponse Dispatch(RequestContext ctx)
    {
        foreach (var route in _routes)
        {
            if (!string.Equals(route.Method, ctx.Method, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!TryMatch(route.Segments, ctx.Path, out var captured))
                continue;
            ctx.RouteValues = captured;
            return route.Handler(ctx);
        }

        return Json.Error(404, ErrorCodes.NotFound, $"no route for {ctx.Method} {ctx.Path}");
    }

    static bool TryMatch(string[] pattern, string path, out Dictionary<string, string> captured)
    {
        captured = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var parts = Split(path);
        if (parts.Length != pattern.Length)
            return false;

        for (var i = 0; i < pattern.Length; i++)
        {
            var p = pattern[i];
            if (p.Length >= 2 && p[0] == '{' && p[p.Length - 1] == '}')
            {
                captured[p.Substring(1, p.Length - 2)] = parts[i];
                continue;
            }

            if (!string.Equals(p, parts[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    static string[] Split(string path)
    {
        if (string.IsNullOrEmpty(path) || path == "/")
            return Array.Empty<string>();
        return path.Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
    }

    readonly struct Route
    {
        public Route(string method, string[] segments, Func<RequestContext, HttpResponse> handler)
        {
            Method = method;
            Segments = segments;
            Handler = handler;
        }

        public string Method { get; }
        public string[] Segments { get; }
        public Func<RequestContext, HttpResponse> Handler { get; }
    }
}
