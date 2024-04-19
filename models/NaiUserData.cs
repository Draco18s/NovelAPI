namespace net.novelai.api
{
    public class NaiUserData
    {
        public string Id { get; set; } // Object ID
        public string Meta { get; set; } // maxLength: 128 - Accompanying non confidential information
        public string Data { get; set; } // Base64-encoded buffer
        public long LastUpdatedAt { get; set; } // UNIX timestamp
        public long ChangeIndex { get; set; } // Incremental revision of the object
        public string Type { get; set; } // minLength: 1, maxLength: 16
    }
}