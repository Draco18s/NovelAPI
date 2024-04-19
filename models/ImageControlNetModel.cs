using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Runtime.Serialization;

namespace net.novelai.api
{
    [Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
    public enum ImageControlNetModel
    {
        [EnumMember(Value = "hed")]
        PaletteSwap,

        [EnumMember(Value = "midas")]
        FormLock,

        [EnumMember(Value = "fake_scribble")]
        Scribbler,

        [EnumMember(Value = "mlsd")]
        BuildingControl,

        [EnumMember(Value = "uniformer")]
        Landscaper,
    }
}