using System;

namespace net.novelai.api
{
    public class NaiAccountInformationResponse : NaiApiError
    {
        public bool EmailVerified { get; set; }
        public bool EmailVerificationLetterSent { get; set; }
        public bool TrialActivated { get; set; }
        public long TrialActionsLeft { get; set; }
        public long TrialImagesLeft { get; set; }
        public long AccountCreatedAt { get; set; }

        public DateTime AccountCreatedDateTime => new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(AccountCreatedAt);

    }
}