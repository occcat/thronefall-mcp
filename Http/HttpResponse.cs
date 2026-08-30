namespace ThronefallControl.Http;

public sealed class HttpResponse
{
    public int Status { get; set; } = 200;
    public string ContentType { get; set; } = "application/json; charset=utf-8";
    public string Body { get; set; } = "";
}
