using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Runtime.Serialization;

namespace net.novelai.api
{
    /// <summary>
    /// Enumerable used to define which Image Generation action the API should perform.
    /// The enum is configured to serialize as a string. (Value specified by EnumMember attribute)
    /// Allows for type-safe handling of string value that is sent to the API.
    /// </summary>
    [Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
    public enum ImageGenerationType
    {
        [EnumMember(Value = "generate")]
        Normal,

        [EnumMember(Value = "img2img")]
        Img2Img,
        
        [EnumMember(Value = "infill")]
        Inpainting,

    }
}