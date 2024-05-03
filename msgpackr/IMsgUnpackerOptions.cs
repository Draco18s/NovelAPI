namespace net.novelai.api.msgpackr
{
    public interface IMsgUnpackerOptions
    {
        bool? UseRecords { get; }
        bool? MapsAsObjects { get; }
        bool? Sequential { get;  }
        bool? Trusted { get;  }
        MsgPackrUnpack.CurrentStructuresModel Structures { get; }
        int? MaxSharedStructures { get;  }
        bool? GetStructures { get;  }
        bool Int64AsNumber { get; }
        string Int64AsType { get; }
        FLOAT32_OPTIONS UseFloat32 { get; }

        public enum FLOAT32_OPTIONS
        {
            NEVER = 0,
            ALWAYS = 1,
            DECIMAL_ROUND = 3,
            DECIMAL_FIT = 4
        }
    }
}