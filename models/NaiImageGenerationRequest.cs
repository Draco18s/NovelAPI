using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;

namespace net.novelai.api
{
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy), ItemNullValueHandling = NullValueHandling.Ignore)]
    public class NaiImageGenerationRequest
    {
        public ImageGenerationType Action { get; set; }
        public string Input { get; set; }
        public ImageGenerationModel Model { get; set; }
        public NaiGenerateImageParameters Parameters { get; set; } = new NaiGenerateImageParameters();
        public string Url { get; set; }
    }
}