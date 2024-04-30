using System.Collections.Generic;

namespace net.novelai.api
{
    public class NaiObjectResponse : NaiApiError
    {
        public IEnumerable<NaiUserData> Objects { get; set; }
    }
}