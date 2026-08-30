using System;
using Newtonsoft.Json.Linq;
using ThronefallControl.Http;
using Xunit;

namespace ThronefallControl.Tests;

public sealed class OpenApiTests
{
    [Fact]
    public void Openapi_json_parses_and_lists_owned_paths()
    {
        var router = Router.CreateDefault();
        var res = router.Dispatch(RequestContext.Create("GET", "/openapi.json"));
        Assert.Equal(200, res.Status);
        Assert.Contains("json", res.ContentType);

        var doc = JObject.Parse(res.Body);
        Assert.Equal(OpenApi.Version, (string?)doc["openapi"]);
        Assert.Equal("Thronefall Control", (string?)doc["info"]?["title"]);

        var paths = doc["paths"] as JObject;
        Assert.NotNull(paths);
        Assert.NotNull(paths!["/health"]);
        Assert.NotNull(paths["/openapi.json"]);
        Assert.NotNull(paths["/state/training"]);
        Assert.NotNull(paths["/state/training"]?["get"]);
        Assert.NotNull(paths["/slots/choice/cancel"]);
        Assert.NotNull(paths["/slots/choice/cancel"]?["post"]);
        Assert.NotNull(paths["/loadout/select"]);
        Assert.NotNull(paths["/level/start"]);
        Assert.NotNull(paths["/king/teleport"]);
        Assert.NotNull(paths["/night/policy"]);
        Assert.NotNull(paths["/debug/upgrade-max"]);
        Assert.NotNull(paths["/debug/skip-wave"]);
        Assert.NotNull(paths["/debug/invulnerable"]);
        Assert.NotNull(paths["/debug/save"]);
        Assert.NotNull(paths["/units/deploy"]);
        Assert.NotNull(paths["/units/deploy"]?["post"]);
        Assert.NotNull(paths["/state/next-wave"]);
        Assert.NotNull(paths["/state/next-wave"]?["get"]);

        Assert.NotNull(paths["/loadout/select"]?["post"]);
        Assert.NotNull(paths["/openapi.json"]?["get"]);
        var policy = (string?)paths["/night/policy"]?["post"]?["summary"];
        Assert.Contains("intent", policy, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("immediately posts units", policy, StringComparison.OrdinalIgnoreCase);

        foreach (var path in paths!.Properties())
        {
            if (path.Value is not JObject methods)
                continue;
            foreach (var method in methods.Properties())
            {
                if (!IsHttpMethod(method.Name))
                    continue;
                var routed = router.Dispatch(RequestContext.Create(method.Name.ToUpperInvariant(), path.Name));
                Assert.DoesNotContain("no route", routed.Body ?? "", StringComparison.Ordinal);
            }
        }

        var ready = router.Dispatch(RequestContext.Create("GET", "/health/ready"));
        Assert.DoesNotContain("no route", ready.Body ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public void Openapi_does_not_need_main_thread()
    {
        Assert.Null(ThronefallControl.Game.MainThread.Current);
        var res = OpenApi.Response();
        Assert.Equal(200, res.Status);
        JObject.Parse(res.Body);
    }

    static bool IsHttpMethod(string name) =>
        name.Equals("get", StringComparison.OrdinalIgnoreCase)
        || name.Equals("post", StringComparison.OrdinalIgnoreCase)
        || name.Equals("put", StringComparison.OrdinalIgnoreCase)
        || name.Equals("patch", StringComparison.OrdinalIgnoreCase)
        || name.Equals("delete", StringComparison.OrdinalIgnoreCase)
        || name.Equals("head", StringComparison.OrdinalIgnoreCase)
        || name.Equals("options", StringComparison.OrdinalIgnoreCase);
}
