using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace net.novelai.api
{
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy),ItemNullValueHandling = NullValueHandling.Ignore)]
    public interface INaiApiError
    {
        int StatusCode { get; set; }
        string Message { get; set; }
    }
}