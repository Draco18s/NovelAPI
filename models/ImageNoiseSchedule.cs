using System.Runtime.Serialization;
using Newtonsoft.Json.Converters;

namespace net.novelai.api
{
    [Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
    public enum ImageNoiseSchedule
    {
        [EnumMember(Value = "native")]
        Native,

        [EnumMember(Value = "karras")]
        Karras,

        [EnumMember(Value = "exponential")]
        Exponential,

        [EnumMember(Value = "polyexponential")]
        PolyExponential,
    }
}