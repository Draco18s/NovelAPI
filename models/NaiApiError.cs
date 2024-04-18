namespace net.novelai.api
{
    public class NaiApiError : INaiApiError
    {
        public int StatusCode { get; set; }
        public string Message { get; set; }
    }
}
