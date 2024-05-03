namespace net.novelai.api.msgpackr
{
    public struct MsgUnpackerOptions : IMsgUnpackerOptions
    {
        public bool? UseRecords { get; set; }
        public bool? MapsAsObjects { get; set; }
        public bool? Sequential { get; set; }
        public bool? Trusted { get; set; }
        public MsgPackrUnpack.CurrentStructuresModel? Structures { get; set; }
        public int? MaxSharedStructures { get; set; }
        public bool? GetStructures { get; set; }
        public bool Int64AsNumber { get; set; }
        public string Int64AsType { get; set; }
        public IMsgUnpackerOptions.FLOAT32_OPTIONS UseFloat32 { get; set; }
        public bool? BundleStrings { get; set; }
        public bool? MoreTypes { get; set; }
        public bool? StructuredClone { get; set; }

    }
}