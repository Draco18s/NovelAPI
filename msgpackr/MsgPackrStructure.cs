using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace net.novelai.api.msgpackr
{
    public delegate JObject MsgPackrStructureReadCallback(object[] args = null);

    public class MsgPackrStructure
    {
        protected MsgPackrUnpack? Reader { get; private set; }
        public IEnumerable<string>? Keys { get; private set; }
        public int? HighByte { get; set; }
        public bool IsShared { get; set; }
        public MsgPackrStructureReadCallback Read { get; protected set; }

        public void AddReadHandler(MsgPackrStructureReadCallback? callback)
        {
            Read = callback;
        }

        public JObject? GenericStructureReadCallback(object[] args = null)
        {
            var json = new StringBuilder("{");
            int count = 0;
            if (Keys != null)
            {
                foreach (string? key in Keys)
                {
                    if (key != null)
                    {
                        if (count++ > 0)
                            json.Append(",");

                        json.Append($"\"{(key == "__proto__" ? "__proto_" : key)}\":null");
                    }
                }
            }
            json.Append("}");
            var obj = JObject.Parse(json.ToString());
            if (Keys != null)
            {
                foreach (string? key in Keys)
                {
                    if (key != null)
                    {

                        var value = Reader?.Read();
                        if (value != null)
                        {
                            obj[key] = Reader.GetJToken(value);
                        }
                    }
                }
            }

            return obj;
        }

        public static T GetNewInstance<T>(MsgPackrUnpack reader, IEnumerable<string?>? keys) where T : MsgPackrStructure, new()
        {
            return new T
            {
                Reader = reader,
                Keys = keys
            };
        }

    }
}