using System;
using System.Collections.Generic;

namespace ThronefallControl.Http;

public sealed class RequestContext
{
    public string Method { get; set; } = "GET";
    public string Path { get; set; } = "/";
    public string QueryString { get; set; } = "";
    public IReadOnlyDictionary<string, string> Query { get; set; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> RouteValues { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, string> Headers { get; set; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public string Body { get; set; } = "";

    public bool DryRun =>
        Query.TryGetValue("dryRun", out var value) && IsTruthy(value);

    public static RequestContext Create(
        string method,
        string url,
        IReadOnlyDictionary<string, string>? headers = null,
        string body = "")
    {
        var q = url.IndexOf('?');
        var path = q < 0 ? url : url.Substring(0, q);
        var qs = q < 0 ? "" : url.Substring(q + 1);
        if (string.IsNullOrEmpty(path))
            path = "/";
        if (!path.StartsWith("/"))
            path = "/" + path;

        return new RequestContext
        {
            Method = method.ToUpperInvariant(),
            Path = path,
            QueryString = qs,
            Query = ParseQuery(qs),
            Headers = NormalizeHeaders(headers),
            Body = body ?? ""
        };
    }

    public string? Header(string name)
    {
        return Headers.TryGetValue(name, out var value) ? value : null;
    }

    static Dictionary<string, string> ParseQuery(string qs)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(qs))
            return map;

        foreach (var part in qs.Split('&'))
        {
            if (part.Length == 0)
                continue;
            var eq = part.IndexOf('=');
            var key = Uri.UnescapeDataString(eq < 0 ? part : part.Substring(0, eq));
            var val = eq < 0 ? "" : Uri.UnescapeDataString(part.Substring(eq + 1).Replace('+', ' '));
            map[key] = val;
        }

        return map;
    }

    static IReadOnlyDictionary<string, string> NormalizeHeaders(IReadOnlyDictionary<string, string>? headers)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (headers == null)
            return map;
        foreach (var kv in headers)
            map[kv.Key] = kv.Value;
        return map;
    }

    static bool IsTruthy(string value) =>
        value is "1" or "true" or "True" or "yes" or "on";
}
