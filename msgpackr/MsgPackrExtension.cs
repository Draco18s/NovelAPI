namespace net.novelai.api.msgpackr
{
    public abstract class MsgPackrExtension : IMsgPackrExtension
    {
        public int Type { get; }
        public bool NoBuffer { get; set; } = false;

        public MsgPackrExtensionUnpackCallback? Unpack { get; private set; }
        public MsgPackrExtensionReadCallback? Read { get; private set; }

        protected MsgPackrUnpack Reader { get; private set; }

        public MsgPackrExtension(int type)
        {
            Type = type;
        }

        protected void AddReadHandler(MsgPackrExtensionReadCallback callback)
        {
            Read = callback;
        }

        protected void AddUnpackHandler(MsgPackrExtensionUnpackCallback callback)
        {
            Unpack = callback;
        }

        public static T GetNewInstance<T>(MsgPackrUnpack reader) where T: MsgPackrExtension, new()
        {
            return new T
            {
                Reader = reader
            };
        }
    }
}