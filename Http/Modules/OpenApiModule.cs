namespace ThronefallControl.Http.Modules;

public sealed class OpenApiModule : IRouteModule
{
    public void Register(Router router)
    {
        router.Map("GET", "/openapi.json", _ => OpenApi.Response());
    }
}