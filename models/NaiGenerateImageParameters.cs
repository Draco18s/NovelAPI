using System;
using System.Collections;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using System.Drawing;
using System.Text.Json.Serialization;
using static net.novelai.api.NaiGenerateImageParameters;
using System.Collections.Generic;

namespace net.novelai.api
{
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy), ItemNullValueHandling = NullValueHandling.Ignore)]
    public class NaiGenerateImageParameters
    {

        [JsonProperty("controlnet_condition")]
        public string ControlnetCondition { get; set; } // Controlnet mask 
        [JsonProperty("controlnet_model")]
        public ImageControlNetModel? ControlnetModel { get; set; } // Model to use for the controlnet
        [JsonProperty("controlnet_strength")]
        public double ControlnetStrength { get; set; } // Influence of the chosen controlnet on the image


        // Reduce the deepfrying effects of high scale (https://twitter.com/Birchlabs/status/1582165379832348672)
        [JsonProperty("dynamic_thresholding")]
        public bool Decrisper { get; set; }


        public string Image { get; set; } // used by img2img


        public bool Legacy { get; set; }
        [JsonProperty("legacy_v3_extend")]
        public bool LegacyV3Extend { get; set; } // Use the old behavior of prompt separation at the 75 tokens mark (can cut words in half)


        public string Mask { get; set; } // Mask for inpainting (Base64-encoded black and white png image, white is the inpainting area)

        [JsonProperty("add_original_image")]
        public bool AddOriginalImage { get; set; } // Prevent seams along the edges of the mask, but may change the image slightly


        [JsonProperty("n_samples")]
        public int NumberOfImagesToGenerate { get; set; } // Number of images to return

        // Combination of user provided and ucPreset content that should not be included in the generated image
        [JsonProperty("negative_prompt")]
        public string NegativePrompt
        {
            get
            {
                var ucDefault = GetPresetString(_model, UndesiredContentPreset);
                return string.IsNullOrWhiteSpace(_uc) ? ucDefault : ucDefault + ", " + _uc;
            }

            set => _uc = value;
        }

        [JsonProperty("params_version")]
        public int ParamsVersion { get; set; }
        public string Prompt { get; set; }


        [JsonProperty("qualityToggle")]
        public bool QualityToggle { get; set; } // https://docs.novelai.net/image/qualitytags.html


        [JsonProperty("reference_image")]
        public string ReferenceImage { get; set; }

        [JsonProperty("reference_information_extracted")]
        public double? ReferenceInformationExtracted { get; set; }
        [JsonProperty("reference_strength")]
        public double? ReferenceStrength { get; set; }


        [JsonProperty("reference_image_multiple")]
        public IEnumerable<string> ReferenceImageMultiple { get; set; }
        [JsonProperty("reference_information_extracted_multiple")]
        public IEnumerable<double> ReferenceInformationExtractedMultiple { get; set; }
        [JsonProperty("reference_strength_multiple")]
        public IEnumerable<double> ReferenceStrengthMultiple { get; set; }

        // https://docs.novelai.net/image/sampling.html
        public ImageSampler Sampler
        {
            get => _sampler;
            set
            {
                _sampler = value;
                if (_sampler == ImageSampler.ddim && _model == ImageGenerationModel.AnimeV3)
                    _sampler = ImageSampler.ddim_v3;
            }
        }

        // https://docs.novelai.net/image/stepsguidance.html (scale is called Prompt Guidance)
        public int Steps { get; set; }
        [JsonProperty("scale")]
        public double PromptGuidance { get; set; } 
        [JsonProperty("uncond_scale")]
        public double? UncondScale { get; set; }

        [JsonProperty("cfg_rescale")]
        public double? CfgRescale { get; set; }    // https://docs.novelai.net/image/stepsguidance.html#prompt-guidance-rescale



        // https://docs.novelai.net/image/sampling.html#special-samplers-smea--smea-dyn
        [JsonProperty("auto_smea")]
        public bool? AutoSmea { get; set; } // Automatically uses SMEA when image is above 1 megapixel

        [JsonProperty("sm")]
        public bool Smea { get; set; }

        [JsonProperty("sm_dyn")]
        public bool SmeaDyn { get; set; }


        // Random seed to use for the image. The nth image has seed + n for seed
        public int Seed
        {
            get => _seed;
            set
            {
                _seed = value;
                if (_seed == 0)
                {
                    _seed = _randomizer.Next(1, Int32.MaxValue - NumberOfImagesToGenerate + 1);
                    _lastSeed = _seed;
                }
                ExtraNoiseSeed = _seed;
            }
        }


        // https://docs.novelai.net/image/strengthnoise.html
        public double Strength { get; set; } 
        public double Noise { get; set; }

        [JsonProperty("noise_schedule")]
        public ImageNoiseSchedule? NoiseSchedule { get; set; }

        [JsonProperty("extra_noise_seed")]
        public int? ExtraNoiseSeed { get; set; }


        [Newtonsoft.Json.JsonIgnore]
        public Size Resolution // Resolution of the image to generate as ImageResolution as a (width, height) tuple
        {
            get => new Size(_width, _height);
            set
            {
                _width = value.Width;
                _height = value.Height;
            }
        }

        public int Height
        {
            get => _height;
            set => _height = value;
        }

        public int Width
        {
            get => _width;
            set => _width = value;
        }


        private ImageGenerationModel _model;
        private int _height;
        private int _width;
        private string _uc;
        private int _seed;
        private int _lastSeed;
        private readonly Random _randomizer = new Random();
        private ImageSampler _sampler;

        [JsonProperty("ucPreset")]
        public UCPreset UndesiredContentPreset { get; set; }

        public NaiGenerateImageParameters() : this(ImageGenerationModel.InpaintingAnimeCurated) { }

        public NaiGenerateImageParameters(ImageGenerationModel model)
        {
            _model = model;
            switch (_model)
            {
                case ImageGenerationModel.AnimeCurated:
                case ImageGenerationModel.AnimeFull:
                case ImageGenerationModel.Furry:
                case ImageGenerationModel.InpaintingAnimeCurated:
                case ImageGenerationModel.InpaintingAnimeFull:
                case ImageGenerationModel.InpaintingFurry:
                    Resolution = ImageResolution.NormalPortrait;
                    PromptGuidance = 10;
                    Sampler = ImageSampler.k_euler_ancestral;
                    Steps = 28;
                    NumberOfImagesToGenerate = 1;
                    Strength = 0.7;
                    Noise = 0;
                    UndesiredContentPreset = UCPreset.Preset_Low_Quality_Bad_Anatomy;
                    QualityToggle = true;
                    Smea = false;
                    SmeaDyn = false;
                    Decrisper = false;
                    ControlnetStrength = 1;
                    Legacy = false;
                    AddOriginalImage = true;
                    LegacyV3Extend = false;
                    ParamsVersion = 1;
                    Seed = 0;
                    _uc = "";
                    break;
                case ImageGenerationModel.AnimeV2:
                    Resolution = ImageResolution.NormalPortraitV2;
                    PromptGuidance = 10;
                    Sampler = ImageSampler.k_euler_ancestral;
                    Steps = 28;
                    NumberOfImagesToGenerate = 1;
                    Strength = 0.7;
                    Noise = 0;
                    UndesiredContentPreset = UCPreset.Preset_Heavy;
                    QualityToggle = true;
                    AutoSmea = true;
                    Smea = false; 
                    SmeaDyn = false;
                    Decrisper = false;
                    ControlnetStrength = 1;
                    Legacy = false;
                    AddOriginalImage = true;
                    UncondScale = 1;
                    CfgRescale = 0;
                    NoiseSchedule = ImageNoiseSchedule.Native;
                    LegacyV3Extend = false;
                    ParamsVersion = 1;
                    Seed = 0;
                    _uc = "";
                    break;
                case ImageGenerationModel.AnimeV3:
                case ImageGenerationModel.InpaintingAnimeV3:
                    Resolution = ImageResolution.NormalPortraitV3;
                    PromptGuidance = 5;
                    Sampler = ImageSampler.k_euler;
                    Steps = 28;
                    NumberOfImagesToGenerate = 1;
                    Strength = 0.7;
                    Noise = 0;
                    UndesiredContentPreset = UCPreset.Preset_Heavy;
                    QualityToggle = true;
                    AutoSmea = true;
                    Smea = false;
                    SmeaDyn = false;
                    Decrisper = false;
                    ControlnetStrength = 1;
                    Legacy = false;
                    AddOriginalImage = true;
                    UncondScale = 1;
                    CfgRescale = 0;
                    NoiseSchedule = ImageNoiseSchedule.Native;
                    LegacyV3Extend = false;
                    ParamsVersion = 1;

                    Seed = 0;
                    _uc = "";
                    break;
            }

            
        }

        private string GetPresetString(ImageGenerationModel model, UCPreset uc)
        {
            switch (model)
            {
                case ImageGenerationModel.AnimeCurated:
                case ImageGenerationModel.InpaintingAnimeCurated:
                    switch (uc)
                    {
                        case UCPreset.Preset_Low_Quality_Bad_Anatomy:
                            return "nsfw, lowres, bad anatomy, bad hands, text, error, " +
                                   "missing fingers, extra digit, fewer digits, cropped, worst quality, low quality, normal quality, " +
                                   "jpeg artifacts, signature, watermark, username, blurry";
                        case UCPreset.Preset_Bad_Anatomy:
                            return null;
                        case UCPreset.Preset_Low_Quality:
                            return "nsfw, lowres, text, cropped, worst quality, low quality, normal quality, " +
                                   "jpeg artifacts, signature, watermark, twitter username, blurry";
                        case UCPreset.Preset_None:
                            return "lowres";
                    }
            break;
                case ImageGenerationModel.AnimeFull:
                case ImageGenerationModel.InpaintingAnimeFull:
                    switch (uc)
                    {
                        case UCPreset.Preset_Low_Quality_Bad_Anatomy:
                            return "nsfw, lowres, bad anatomy, bad hands, text, error, " +
                                   "missing fingers, extra digit, fewer digits, cropped, worst quality, low quality, normal quality, " +
                                   "jpeg artifacts, signature, watermark, username, blurry";
                        case UCPreset.Preset_Bad_Anatomy:
                            return null;
                        case UCPreset.Preset_Low_Quality:
                            return "nsfw, lowres, text, cropped, worst quality, low quality, normal quality, " +
                                   "jpeg artifacts, signature, watermark, twitter username, blurry";
                        case UCPreset.Preset_None:
                            return "lowres";

                    }
            break;
                case ImageGenerationModel.Furry:
                case ImageGenerationModel.InpaintingFurry:
                    switch (uc)
                    {
                        case UCPreset.Preset_Low_Quality_Bad_Anatomy:
                            return null;
                        case UCPreset.Preset_Low_Quality:
                            return "nsfw, worst quality, low quality, what has science done, what, " +
                                   "nightmare fuel, eldritch horror, where is your god now, why";
                        case UCPreset.Preset_Bad_Anatomy:
                            return "{worst quality}, low quality, distracting watermark, [nightmare fuel], " +
                                   "{{unfinished}}, deformed, outline, pattern, simple background";
                        case UCPreset.Preset_None:
                            return "low res";

                    }
            break;
                case ImageGenerationModel.AnimeV2:
                    switch (uc)
                    {
                        case UCPreset.Preset_Heavy:
                            return "nsfw, lowres, bad, text, error, missing, extra, fewer, cropped, jpeg artifacts, " +
                                   "worst quality, bad quality, watermark, displeasing, unfinished, chromatic aberration, scan, " +
                                   "scan artifacts";
                        case UCPreset.Preset_Light:
                            return "nsfw, lowres, jpeg artifacts, worst quality, watermark, blurry, very displeasing";
                        case UCPreset.Preset_None:
                            return "lowres";
                    }
            break;
                case ImageGenerationModel.AnimeV3:
                case ImageGenerationModel.InpaintingAnimeV3:
                    switch (uc)
                    {
                        case UCPreset.Preset_Heavy:
                            return "nsfw, lowres, {bad}, error, fewer, extra, missing, worst quality, jpeg artifacts, " +
                                   "bad quality, watermark, unfinished, displeasing, chromatic aberration, signature, extra digits, " +
                                   "artistic error, username, scan, [abstract]";
                        case UCPreset.Preset_Light:
                            return "nsfw, lowres, jpeg artifacts, worst quality, watermark, blurry, very displeasing";
                        case UCPreset.Preset_None:
                            return "lowres";
                    }
            break;
            }

            return null;
        }



        /// <summary>
        /// Presets for Undesired Content
        /// </summary>
        public enum UCPreset
        {
            Preset_Low_Quality_Bad_Anatomy = 0,
            Preset_Low_Quality = 1,
            Preset_Bad_Anatomy = 2,
            Preset_None = 3,
            Preset_Heavy = 4,
            Preset_Light = 5,
        }
    }
}