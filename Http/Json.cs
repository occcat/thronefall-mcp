using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using ThronefallControl.Dto;

namespace ThronefallControl.Http;

public static class Json
{
    public static readonly JsonSerializerSettings Settings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Include,
        Formatting = Formatting.None
    };

    public static string Serialize(object value) =>
        JsonConvert.SerializeObject(value, Settings);

    public static T? Deserialize<T>(string json) =>
        JsonConvert.DeserializeObject<T>(json, Settings);

    public static HttpResponse Ok(object body, int status = 200) =>
        new() { Status = status, Body = Serialize(body) };

    public static HttpResponse Error(
        int status,
        string error,
        string message,
        string? phase = null,
        int? generation = null) =>
        new()
        {
            Status = status,
            Body = Serialize(new ErrorResponse
            {
                Ok = false,
                Error = error,
                Message = message,
                Phase = phase,
                Generation = generation
            })
        };
}
