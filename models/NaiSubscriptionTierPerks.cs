namespace net.novelai.api
{
    public class NaiSubscriptionTierPerks : NaiApiError
    {
        public long MaxPriorityActions { get; set; } //Amount of max priority actions
        public long StartPriority { get; set; } // Start priority amount
        public long ModuleTrainingSteps { get; set; } // Amount of module training steps granted every month
        public bool UnlimitedMaxPriority { get; set; } // Is max priority unlimited

        public bool VoiceGeneration { get; set; }
        public bool ImageGeneration { get; set; }
        public bool UnlimitedImageGeneration { get; set; }
        public ImageGenerationLimits[] UnlimitedImageGenerationLimits { get; set; }
        public long ContextTokens { get; set; } //Amount of granted context tokens

        public class ImageGenerationLimits
        {
            public long Resolution { get; set; }
            public int MaxPrompts { get; set; }
        }

    }
}