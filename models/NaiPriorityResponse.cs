using System;

namespace net.novelai.api
{
    public class NaiPriorityResponse : NaiApiError
    {
        public long MaxPriorityActions { get; set; }
        public long NextRefillAt { get; set; }
        public long TaskPriority { get; set; }
        public DateTime NextRefillDateTime => new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(NextRefillAt);
    }
}