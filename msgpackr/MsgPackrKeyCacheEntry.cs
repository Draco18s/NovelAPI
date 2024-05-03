using System.Collections.Generic;

namespace net.novelai.api.msgpackr
{
    public class MsgPackrKeyCacheEntry : List<long>
    {
        public long Bytes { get; set; }
        public string? String { get; set; }

    }
}