using System.Runtime.Serialization;
using Newtonsoft.Json.Converters;

namespace net.novelai.api
{
    /// <summary>
    /// Enumerable used to define the objects that can be used with the /user/objects/{type} endpoint.
    /// The enum is configured to serialize as a string. (Value specified by EnumMember attribute)
    /// Allows for type-safe handling of string value that is sent to the API.
    /// </summary>
    [Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
    public enum UserObjectType
    {
        [EnumMember(Value = "stories")]
        Stories,

        [EnumMember(Value = "storycontent")]
        StoryContent,

        [EnumMember(Value = "presets")]
        Presets,

        [EnumMember(Value = "aimodules")]
        AiModules,

        [EnumMember(Value = "shelf")]
        Shelf,
    }
}