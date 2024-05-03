namespace net.novelai.api.msgpackr
{
    public delegate object? MsgPackrExtensionUnpackCallback(byte[] bytes);

    public delegate object? MsgPackrExtensionReadCallback(object[] args = null);


    public interface IMsgPackrExtension
    {
        int Type { get; }
        bool NoBuffer { get; set; }

        public MsgPackrExtensionUnpackCallback? Unpack { get; }
        public MsgPackrExtensionReadCallback? Read { get; }
    }
}