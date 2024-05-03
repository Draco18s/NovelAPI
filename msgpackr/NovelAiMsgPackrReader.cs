using System;
using System.Reflection;
using Newtonsoft.Json.Linq;


namespace net.novelai.api.msgpackr
{

    public class NovelAiMsgUnpacker : MsgPackrUnpack
    {
        public NovelAiMsgUnpacker(MsgUnpackerOptions? options = null) : base(options)
        {
            AddExtension<Ext20>();
            AddExtension<Ext30>();
            AddExtension<Ext31>();
            AddExtension<Ext40>();
            AddExtension<Ext41>();
            AddExtension<Ext42>();
        }

        private static object _readHandler (object[] args) => args?.Length == 1 && args[0] is JToken? args[0] : null;

        // Extension 0x14
        private class Ext20 : MsgPackrExtension
        {
            public Ext20() : base(20)
            {
                AddReadHandler(_readHandler);
            }
        }

        // Extension 0x1E
        private class Ext30 : MsgPackrExtension
        {
            public Ext30() : base(30)
            {
                AddReadHandler(_readHandler);
            }
        }

        // Extension 0x1F
        private class Ext31 : MsgPackrExtension
        {
            public Ext31() : base(31)
            {
                AddReadHandler(_readHandler);
            }
        }

        // Extension 0x28
        private class Ext40 : MsgPackrExtension
        {
            public Ext40() : base(40)
            {
                AddReadHandler(_readHandler);
            }
        }

        // Extension 0x29
        private class Ext41 : MsgPackrExtension
        {
            public Ext41() : base(41)
            {
                AddReadHandler(_readHandler);
            }
        }

        // Extension 0x2A
        private class Ext42 : MsgPackrExtension
        {
            public Ext42() : base(42)
            {
                AddReadHandler(_readHandler);
            }
        }
    }
}