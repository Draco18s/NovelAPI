using System;
using System.Text.Json.Serialization;

namespace net.novelai.api
{
    public class NaiSubscriptionResponse : NaiApiError
    {
        public SubscriptionTier Tier { get; set; } //Subscription internal tier number, see SubscriptionTiers enum

        public bool Active { get; set; } //Is subscription active as of the moment of the request

        public long ExpiresAt { get; set; } //UNIX timestamp of subscription expiration

        public NaiSubscriptionTierPerks Perks { get; set; } // Subscription perks

        [JsonIgnore]
        public string PaymentProcessorData { get; set; } // 
        
        public NaiSubscriptionAvailableTrainingSteps TrainingStepsLeft { get; set; }

        public long Anlas => TrainingStepsLeft.PurchasedTrainingSteps + TrainingStepsLeft.FixedTrainingStepsLeft;
        public DateTime ExpiresAtDateTime => new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(ExpiresAt);

        public class NaiSubscriptionAvailableTrainingSteps
        {
            public long FixedTrainingStepsLeft { get; set; } // Amount of available fixed module training steps left(reset every month)
            public long PurchasedTrainingSteps { get; set; } // Amount of available purchased module training steps left
        }

        public enum SubscriptionTier
        {
            Paper = 0,
            Tablet = 1,
            Scroll = 2,
            Opus = 3
        }

    }
}