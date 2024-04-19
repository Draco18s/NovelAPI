using Newtonsoft.Json.Converters;
using System.Runtime.Serialization;

namespace net.novelai.api
{
    [Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
    public enum ImageSampler
    {
        k_lms,
        k_euler,
        k_euler_ancestral,
        k_heun,
        plms,  // doesn't work
        ddim,
        ddim_v3,  // for v3

        nai_smea,  // doesn't work
        nai_smea_dyn,

        k_dpmpp_2m,
        k_dpmpp_2s_ancestral,
        k_dpmpp_sde,
        k_dpm_2,
        k_dpm_2_ancestral,
        k_dpm_adaptive,
        k_dpm_fast,

    }
}