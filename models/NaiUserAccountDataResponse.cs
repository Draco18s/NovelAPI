namespace net.novelai.api
{
    public class NaiUserAccountDataResponse : NaiApiError
    {
        public NaiPriorityResponse Priority { get; set; }
        public NaiSubscriptionResponse Subscription { get; set; }
        public NaiGetKeystoreResponse Keystore { get; set; }
        public string Settings { get; set; }
        public NaiAccountInformationResponse Information { get; set; }

    }
}