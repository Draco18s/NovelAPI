using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace net.novelai.api
{

    /// <summary>
    /// Enumerable used to define which Image Model the API should use to generate the image.
    /// The enum is configured to serialize as a string. (Value specified by EnumMember attribute)
    /// Allows for type-safe handling of string value that is sent to the API.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ImageGenerationModel
    {
        [EnumMember(Value = "safe-diffusion")]
        AnimeCurated,

        [EnumMember(Value = "nai-diffusion")]
        AnimeFull,

        [EnumMember(Value = "nai-diffusion-furry")]
        Furry,

        [EnumMember(Value = "safe-diffusion-inpainting")]
        InpaintingAnimeCurated,

        [EnumMember(Value = "nai-diffusion-inpainting")]
        InpaintingAnimeFull,

        [EnumMember(Value = "furry-diffusion-inpainting")]
        InpaintingFurry,
        
        [EnumMember(Value = "nai-diffusion-2")]
        AnimeV2,

        [EnumMember(Value = "nai-diffusion-3")]
        AnimeV3,

        [EnumMember(Value = "nai-diffusion-3-inpainting")]
        InpaintingAnimeV3
    }
}