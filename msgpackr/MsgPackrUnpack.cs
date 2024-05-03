/*
 * MessagePack Unpacker v1.10.0
 *
 * Ported from the msgpackr v1.10.0 unpack.js code found at https://github.com/kriszyp/msgpackr
 * Original javascript code copyright 2020 Kris Zyp, and released under the MIT license
 *
 * Ported to C# by Jason L. Walker - 2024
 * 
 * Can roughly use the specification found at https://github.com/msgpack/msgpack/blob/master/spec.md
 * as a reference.
 *
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace net.novelai.api.msgpackr
{
    public class MsgPackrUnpack : IMsgUnpackerOptions
    {
        #region Member variables

        private ReadOnlyMemory<byte> src;
        private int srcEnd = 0;
        private int position;

        private readonly List<string> strings = new List<string>();
        private int stringPosition = 0;
        private IMsgUnpackerOptions currentUnpackr = null;

        private CurrentStructuresModel _currentStructures = new CurrentStructuresModel();
        private ReadOnlyMemory<byte>? srcString;
        private int srcStringStart = 0;
        private int srcStringEnd = 0;
        private BundledStringModel bundledStrings;
        private object referenceMap;

        private readonly Dictionary<int, IMsgPackrExtension> _currentExtensions = new Dictionary<int, IMsgPackrExtension>();
        private MsgUnpackerOptions defaultOptions = JsonConvert.DeserializeObject<MsgUnpackerOptions>("{useRecords: false}");

        protected readonly C1Type C1 = new C1Type();
        private bool sequentialMode = false;
        private int inlineObjectReadThreshold = 2;
        private object readstruct;
        private object onLoadedStructures;
        private object onSaveState;

        private CurrentStructuresModel structures;
        private Regex validName = new Regex("^[a-zA-Z_$][a-zA-Z\\d_$]*$");
        private bool isNativeAccelerationEnabled = false;
        private MsgPackrKeyCacheEntry[] KeyCache = new MsgPackrKeyCacheEntry?[4096];

        public bool? UseRecords { get; protected set; }
        public bool? MapsAsObjects { get; protected set; }
        public bool? Sequential { get; protected set; }
        public bool? Trusted { get; protected set; }
        public CurrentStructuresModel Structures { get; protected set; }
        public int? MaxSharedStructures { get; protected set; }
        public bool? GetStructures { get; protected set; }
        public bool Int64AsNumber { get; protected set; }
        public string Int64AsType { get; protected set; }
        public IMsgUnpackerOptions.FLOAT32_OPTIONS UseFloat32 { get; protected set; }

        protected string[] Mult10;

        public ReadOnlyMemory<byte> Buffer => src;
        public int Offset => position;

        protected Dictionary<int, MsgPackrStructure> CurrentStructures => _currentStructures;
        protected Dictionary<int, IMsgPackrExtension> CurrentExtensions => _currentExtensions;
        public object Value { get; private set; }
        #endregion

        #region class models
        public class CurrentStructuresModel : Dictionary<int, MsgPackrStructure>
        {
            public Dictionary<int, MsgPackrStructure> RestoreStructures { get; set; }
            public int? SharedLength { get; set; }
            public bool? Uninitialized { get; set; }
        }

        public class C1Type
        {
            public string name => "MessagePack 0xC1";
        }

        public class BundledStringModel
        {
            public string[] Strings { get; } = new string[2];

            public int Position0 { get; set; }
            public int Position1 { get; set; }
            public int PostBundlePosition { get; set; }
        }

        public struct UnpackOptions
        {
            public int? Start;
            public int? End;
        }
        #endregion

        #region Contructor / Initialization
        public MsgPackrUnpack(MsgUnpackerOptions? opts = null)
        {
            var mult10 = new string[256];
            for (int i = 0; i < 256; i++)
            {
                mult10[i] = JValue.Parse($"1e{Math.Floor(45.15 - i * 0.30103)}").ToString();
            }
            Mult10 = mult10;

            // Register tagged extensions
            AddExtension<Ext0>();
            AddExtension<Ext66>();
            AddExtension<Ext98>();
            AddExtension<Ext101>();
            AddExtension<Ext105>();
            AddExtension<Ext112>();
            AddExtension<Ext115>();
            AddExtension<Ext116>();
            AddExtension<Ext120>();
            AddExtension<Ext255>();

            //var options = null;

            if (opts is MsgUnpackerOptions options)
            {
                if (options.UseRecords == false && options.MapsAsObjects == null)
                    options.MapsAsObjects = true;
                if (options.Sequential != null && options.Trusted != false)
                {
                    options.Trusted = true;
                    if (options.Structures == null  && options.UseRecords != false)
                    {
                        options.Structures = new CurrentStructuresModel();
                        if (options.MaxSharedStructures == null)
                            options.MaxSharedStructures = 0;
                    }
                }
            
                if (options.Structures != null)
                    options.Structures.SharedLength = options.Structures.Count;
                else if (options.GetStructures != null)
                {
                    options.Structures = new CurrentStructuresModel();
                    options.Structures.Uninitialized = true; // this is what we use to denote an uninitialized structures
                    options.Structures.SharedLength = 0;
                }
                if (options.Int64AsNumber)
                {
                    options.Int64AsType = "number";
                }

                UseRecords = options.UseRecords;
                MapsAsObjects = options.MapsAsObjects;
                Sequential = options.Sequential;
                Trusted = options.Trusted;
                Structures = options.Structures;
                MaxSharedStructures = options.MaxSharedStructures;
                GetStructures = options.GetStructures;
                Int64AsNumber = options.Int64AsNumber;
                Int64AsType = options.Int64AsType;
                UseFloat32 = options.UseFloat32;
                bundledStrings = options.BundleStrings ?? false ? new BundledStringModel() : bundledStrings;

            }
        }
        #endregion

        public virtual object Unpack(ReadOnlyMemory<byte> source, object opts = null)
        {
            if (src.Length > 0)
            {
                // re-entrant execution, save the state and restore it after we do this unpack
                return SaveState(/*() =>
                {
                    ClearSource();
                    return Unpack(source, opts);
                }*/);
            }

            if (opts != null && opts is UnpackOptions options)
            {
                srcEnd = options.End ?? source.Length;
                position = options.Start ?? 0;
            }
            else
            {
                position = 0;
                srcEnd = (opts as int?) > -1 ? (int)opts : source.Length;
            }
            stringPosition = 0;
            srcStringEnd = 0;
            srcString = null;
            strings.Clear();
            bundledStrings = null;
            src = source;

            if (currentUnpackr == null)
            {
                currentUnpackr = defaultOptions;
                if (_currentStructures.Count > 0)
                {
                    _currentStructures.Clear();
                }
            }
        
            if (structures != null)
            {
                _currentStructures.Clear();
                foreach (var structure in structures)
                {
                    _currentStructures[structure.Key] = structure.Value;
                }
                return CheckedRead(opts);
            } 
        
            if (_currentStructures.Count > 0)
            {
                _currentStructures.Clear();
            }
            return CheckedRead(opts);
        }

        public object UnpackMultiple(byte[] source, object? forEach)
        {
            // Todo: Implement this:
            /*
            let values, lastPosition = 0
               try {
                sequentialMode = true
                let size = source.length
                let value = this ? this.unpack(source, size) : defaultUnpackr.unpack(source, size)
                if (forEach) {
                    if (forEach(value, lastPosition, position) === false) return;
                    while(position < size) {
                        lastPosition = position
                        if (forEach(checkedRead(), lastPosition, position) === false) {
                            return
                        }
                    }
                }
                else {
                    values = [ value ]
                    while(position < size) {
                        lastPosition = position
                        values.push(checkedRead())
                    }
                    return values
                }
               } catch(error) {
                error.lastPosition = lastPosition
                error.values = values
                throw error
               } finally {
                sequentialMode = false
                clearSource()
               }

             */
            throw new NotImplementedException();
        }
    
        #region custom tagged extensions
        // Extension 0x00
        // notepack defines extension 0 to mean undefined, so use that as the default here
        private class Ext0 : MsgPackrExtension 
        { 
            public Ext0() : base(0)
            {
                NoBuffer = true;
                AddUnpackHandler((byte[] bytes) => null);
            }
        }

        // Extension 0x42
        private class Ext66 : MsgPackrExtension { 
            public Ext66() : base(66)
            {
                AddUnpackHandler((byte[] bytes) =>
                {
                    /*
                    // decode bigint
                    let length = data.length;
                    let value = BigInt(data[0] & 0x80 ? data[0] - 0x100 : data[0]);
                    for (let i = 1; i < length; i++)
                    {
                        value <<= 8n;
                        value += BigInt(data[i]);
                    }
                    return value;
                    */
                    throw new NotImplementedException();
                });
            }
        }


        // Extension 0x62
        private class Ext98 : MsgPackrExtension
        {
            public Ext98() : base(98)
            {
                AddUnpackHandler((byte[] args) =>
                {
                    if (args is null)
                        throw new ArgumentException("Invalid argument for extension 98");
                    var offset = Reader.Offset;
                    var origOffset = offset;
                    var data = args ?? new byte[] { };
                    var n = (data[0] << 24) + (data[1] << 16) + (data[2] << 8) + data[3];
                    offset += n - data.Length;
                    Reader.Seek(offset);
                    var model = new BundledStringModel();
                    Reader.SetBundledStrings(model);
                    model.Strings[0] = Reader.ReadOnlyJsString() ?? "";
                    model.Strings[1] = Reader.ReadOnlyJsString() ?? "";
                    model.Position0 = 0;
                    model.Position1 = 0;
                    model.PostBundlePosition = Reader.Offset;
                    Reader.Seek(origOffset);
                    return Reader.Read();
                });
            }
        }
        protected void SetBundledStrings(BundledStringModel model)
        {
            bundledStrings = model;
        }


        // Extension 0x65
        private class Ext101 : MsgPackrExtension { public Ext101() : base(101)
            {
                AddUnpackHandler((byte[] bytes) =>
                {
                    /*
                    let data = read()
                	return (errors[data[0]] || Error)(data[1])
                    */
                    throw new NotImplementedException();
                });
            }
        }
        // Extension 0x69
        private class Ext105 : MsgPackrExtension { public Ext105() : base(105)
            {
                AddUnpackHandler((byte[] bytes) =>
                {
                    /*
                     	// id extension (for structured clones)
	                    if (currentUnpackr.structuredClone === false) throw new Error('Structured clone extension is disabled')
	                    let id = dataView.getUint32(position - 4)
	                    if (!referenceMap)
		                    referenceMap = new Map()
	                    let token = src[position]
	                    let target
	                    // TODO: handle Maps, Sets, and other types that can cycle; this is complicated, because you potentially need to read
	                    // ahead past references to record structure definitions
	                    if (token >= 0x90 && token < 0xa0 || token == 0xdc || token == 0xdd)
		                    target = []
	                    else
		                    target = {}

	                    let refEntry = { target } // a placeholder object
	                    referenceMap.set(id, refEntry)
	                    let targetProperties = read() // read the next value as the target object to id
	                    if (refEntry.used) // there is a cycle, so we have to assign properties to original target
		                    return Object.assign(target, targetProperties)
	                    refEntry.target = targetProperties // the placeholder wasn't used, replace with the deserialized one
	                    return targetProperties // no cycle, can just use the returned read object

                     */
                    throw new NotImplementedException();
                });
            }
        }
        // Extension 0x70
        private class Ext112 : MsgPackrExtension { public Ext112() : base(112)
            {
                AddUnpackHandler((byte[] bytes) =>
                {
                    /*
	                // pointer extension (for structured clones)
	                if (currentUnpackr.structuredClone === false) throw new Error('Structured clone extension is disabled')
	                let id = dataView.getUint32(position - 4)
	                let refEntry = referenceMap.get(id)
	                refEntry.used = true
	                return refEntry.target
                     */
                    throw new NotImplementedException();
                });
            }
        }
        // Extension 0x73
        private class Ext115 : MsgPackrExtension { public Ext115() : base(115)
            {
                AddUnpackHandler((byte[] bytes) =>
                {
                    var value = Reader.Read();
                    if (value.GetType().IsArray)
                        return Reader.GetJToken(value);
                    
                    return Reader.GetJToken(new object[]{});
                });
            }
        }
        // Extension 0x74
        private static string[] typedArrays = new string[]{
            "Int8", "Uint8", "Uint8Clamped", "Int16", "Uint16", "Int32", "Uint32", "Float32", "Float64", "BigInt64",
            "BigUint64"
        };//.map(type => type + 'Array')
        //let glbl = typeof globalThis === 'object' ? globalThis : window;

        private class Ext116 : MsgPackrExtension { public Ext116() : base(116)
            {
                AddUnpackHandler((byte[] bytes) =>
                {
                    /*
	                let typeCode = data[0]
	                let typedArrayName = typedArrays[typeCode]
	                if (!typedArrayName)
		                throw new Error('Could not find typed array for code ' + typeCode)
	                // we have to always slice/copy here to get a new ArrayBuffer that is word/byte aligned
	                return new glbl[typedArrayName](Uint8Array.prototype.slice.call(data, 1).buffer)
                     */
                    throw new NotImplementedException();
                });
            }
        }
        // Extension 0x78
        private class Ext120 : MsgPackrExtension { public Ext120() : base(120)
            {
                AddUnpackHandler((byte[] bytes) =>
                {
                    /*
	                let data = read()
	                return new RegExp(data[0], data[1])
                     */
                    throw new NotImplementedException();
                });
            }
        }
        // Extension 0xff
        private class Ext255 : MsgPackrExtension { public Ext255() : base(255)
            {
                AddUnpackHandler((byte[] data) =>
                {
                    /*
                     MessagePack's date/Time specs:
                     
                       timestamp 32 stores the number of seconds that have elapsed since 1970-01-01 00:00:00 UTC
                       in an 32-bit unsigned integer:
                       +--------+--------+--------+--------+--------+--------+
                       |  0xd6  |   -1   |   seconds in 32-bit unsigned int  |
                       +--------+--------+--------+--------+--------+--------+
                       
                       timestamp 64 stores the number of seconds and nanoseconds that have elapsed since 1970-01-01 00:00:00 UTC
                       in 32-bit unsigned integers:
                       +--------+--------+--------+--------+--------+------|-+--------+--------+--------+--------+
                       |  0xd7  |   -1   | nanosec. in 30-bit unsigned int |   seconds in 34-bit unsigned int    |
                       +--------+--------+--------+--------+--------+------^-+--------+--------+--------+--------+
                       
                       timestamp 96 stores the number of seconds and nanoseconds that have elapsed since 1970-01-01 00:00:00 UTC
                       in 64-bit signed integer and 32-bit unsigned integer:
                       +--------+--------+--------+--------+--------+--------+--------+
                       |  0xc7  |   12   |   -1   |nanoseconds in 32-bit unsigned int |
                       +--------+--------+--------+--------+--------+--------+--------+
                       +--------+--------+--------+--------+--------+--------+--------+--------+
                       |                   seconds in 64-bit signed int                        |
                       +--------+--------+--------+--------+--------+--------+--------+--------+
                     */


                    long milliseconds = 0;
                    long nanoseconds = 0;
                    DateTime? dateTime = null;
	                // 32-bit date extension
                    if (data.Length == 4)
                    {
                        dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                        milliseconds = ((data[3] & 0x3) * 0x100000000 + data[4] * 0x1000000 + (data[5] << 16) + (data[6] << 8) + data[7]) * 1000;
                        dateTime = dateTime?.AddMilliseconds(milliseconds);
                        return dateTime;
                        //return new DateTime((data[0] * 0x1000000 + (data[1] << 16) + (data[2] << 8) + data[3]) * 1000);
                    }
                    else if (data.Length == 8)
                    {
                        dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                        nanoseconds = ((data[0] << 22) + (data[1] << 14) + (data[2] << 6) + (data[3] >> 2)) / 100000;
                        milliseconds = ((data[3] & 0x3) * 0x100000000 + data[4] * 0x1000000 + (data[5] << 16) + (data[6] << 8) + data[7]) * 1000;
                        dateTime = dateTime?.AddMilliseconds(milliseconds);
                        dateTime = dateTime?.AddMilliseconds(nanoseconds);
                        return dateTime;
                        /*new DateTime(
                            ((data[0] << 22) + (data[1] << 14) + (data[2] << 6) + (data[3] >> 2)) / 1000000 +
                            ((data[3] & 0x3) * 0x100000000 + data[4] * 0x1000000 + (data[5] << 16) + (data[6] << 8) +
                             data[7]) * 1000);*/
                    }
                    else if (data.Length == 12) // TODO: Implement support for negative
                        /*
                            return new DateTime(
                                ((data[0] << 24) + (data[1] << 16) + (data[2] << 8) + data[3]) / 1000000 +
                                (((data[4] & 0x80) > 0 ? -0x1000000000000 : 0) + data[6] * 0x10000000000 +
                                 data[7] * 0x100000000 + data[8] * 0x1000000 + (data[9] << 16) + (data[10] << 8) +
                                 data[11]) * 1000);
                        */
                        throw new NotImplementedException();
                    else
                        return null;
                });
            }
        }
        #endregion

        protected IEnumerable<MsgPackrStructure> _mergeStructures(object loadedStructures, object existingStructures)
        {
            /*
            if (onLoadedStructures)
                loadedStructures = onLoadedStructures.call(this, loadedStructures);
               loadedStructures = loadedStructures || []
               if (Object.isFrozen(loadedStructures))
                loadedStructures = loadedStructures.map(structure => structure.slice(0))
               for (let i = 0, l = loadedStructures.length; i < l; i++) {
                let structure = loadedStructures[i]
                if (structure) {
                    structure.isShared = true
                    if (i >= 32)
                        structure.highByte = (i - 32) >> 5
                }
               }
               loadedStructures.sharedLength = loadedStructures.length
               for (let id in existingStructures || []) {
                if (id >= 0) {
                    let structure = loadedStructures[id]
                    let existing = existingStructures[id]
                    if (existing) {
                        if (structure)
                            (loadedStructures.restoreStructures || (loadedStructures.restoreStructures = []))[id] = structure
                        loadedStructures[id] = existing
                    }
                }
               }
               return this.structures = loadedStructures
             */
            throw new NotImplementedException();
        }

        public object? Decode(byte[] source, object? options)
        {
            return Unpack(source, options);
        }

        public int GetPosition() => position;

        public object CheckedRead(object? options)
        {
            try {
                if (!(currentUnpackr?.Trusted ?? false) && !sequentialMode)
                {
                    int sharedLength = _currentStructures?.SharedLength ?? 0;

                    if (sharedLength < _currentStructures?.Count)
                    {
                        // Todo: Implement the following:
                        //_currentStructures.Count = sharedLength;
                        throw new NotImplementedException();
                    }
                }

                object result;
                // Todo: Implement the following:
                /*
                if ((currentUnpackr?.RandomAccessStructure ?? false) && ReadByte(position) < 0x40 && ReadByte(position) >= 0x20 && ReadStruct)
                {
                    result = ReadStruct(src, position, srcEnd, currentUnpackr);
                    src = null; // dispose of this so that recursive unpack calls don't save state
                    if (!(options && options.Lazy) && result)
                        result = result.toJSON()
                    position = srcEnd;
                }
                else
                */
                result = Read();
                if (bundledStrings != null) { // bundled strings to skip past
                    position = bundledStrings.PostBundlePosition;
                    bundledStrings = null;
                }
            
                if (sequentialMode)
                    // we only need to restore the structures if there was an error, but if we completed a read,
                    // we can clear this out and keep the structures we read
                    _currentStructures.RestoreStructures = null;
            
                if (position == srcEnd) {
                    // finished reading this source, cleanup references
                    if (_currentStructures?.RestoreStructures != null && _currentStructures.Count > 0)
                        RestoreStructures();
                    _currentStructures?.Clear();
                    src = null;
                    if (referenceMap != null)
                        referenceMap = null;
                } else if (position > srcEnd) {
                    // over read
                    throw new Exception("Unexpected end of MessagePack data");
                } else if (!sequentialMode) {
                    string? jsonView = null;
                    // Todo: Implement the following:
                    /*
                    try {
                        jsonView = JSON.stringify(result, (_, value) => typeof value === "bigint" ? `${value}n` : value).slice(0, 100)
                    } catch(error) {
                        jsonView = '(JSON view not available ' + error + ')'
                    }
                    */
                    throw new Exception("Data read, but end of buffer not reached " + (jsonView ?? ""));
                }
                // else more to read, but we are reading sequentially, so don't clear source yet
                return result;
            } catch(Exception error)
            {
            
                if (_currentStructures.RestoreStructures != null)
                    RestoreStructures();
                ClearSource();
  
                throw error;
            }        
        }

        protected void RestoreStructures()
        {
            if (_currentStructures?.RestoreStructures != null)
            {
                foreach (var kvp in _currentStructures.RestoreStructures) {
                    _currentStructures[kvp.Key] = kvp.Value;
                }
                _currentStructures.RestoreStructures = null;
            }
        }

        public virtual object Read()
        {
            byte? token = ReadByte(position++);
            if (token < 0xa0) // Less than 0xa0 Represents primitives or arrays
            {
                if (token < 0x80) // Less than 0x80 represents primitives
                {
                    if (token < 0x40) // Less than 0x40 represents integers
                    {
                        return token;
                    }
                    else
                    {
                        var structure = _currentStructures.FirstOrDefault(o => o.Key == ((token ?? 0))).Value;
                        

                        if (structure != null || ((currentUnpackr.GetStructures ?? false ) && (LoadStructures()[(token ?? 0) & 0x3f] != null)))
                        {
                            //var structure = _currentStructures.FirstOrDefault(o => o.Key == ((token ?? 0) & 0x3F)); // Apply mask
                            //ToDo: finish implementing
                            /*
                            var structure = _currentStructures[(token ?? 0) & 0x3f] ??
                                            currentUnpackr?.GetStructures && LoadStructures()[(token ?? 0) & 0x3f];
                            */
                            if (structure != null) { 
                                if (structure.Read == null)
                                {
                                    structure.AddReadHandler(CreateStructureReader(structure, token & 0x3f));
                                }

                                return structure.Read();
                            }
                        }
                        else
                        {
                            return token;
                        }
                        throw new NotImplementedException();
                    }
                }
                else if (token < 0x90)
                {
                    // map
                    token -= 0x80;
                    if (currentUnpackr?.MapsAsObjects ?? false)
                    {
                        JObject obj = new JObject();
                        for (var i = 0; i < token; i++)
                        {
                            var key = ReadKey() ?? "";
                            if (key == "__proto__")
                                key = "__proto_";
                            obj[key] = GetJToken(Read() ?? new {});
                        }

                        return obj;
                    } 
                    else
                    {
                        var map = new JObject();
                        for (var i = 0; i < token; i++)
                        {
                            string key = Read()?.ToString();
                            object value = Read();
                            if (!string.IsNullOrWhiteSpace(key))
                            {
                                map[key] = null;
                                if (value != null)
                                {
                                    map[key] = GetJToken(value);
                                }
                            }
                        }
                        return map;
                    }
                }
                else
                {
                    token -= 0x90;
                    var array = new object[(token ?? 0)];
                    for (var i = 0; i < token; i++)
                    {
                        array[i] = Read();
                    }
                    /*
                    if (currentUnpackr.FreezeData)
                        return Object.freeze(array)
                    */
                    return array;
                }
            }
            else if (token < 0xc0)
            {
                // fixstr
                var length = token - 0xa0;
                if (srcStringEnd >= position)
                {
                    //return srcString.slice(_offset - srcStringStart, (_offset += length) - srcStringStart);
                    throw new NotImplementedException();
                }
                if (srcStringEnd == 0 && srcEnd < 140)
                {
                    // for small blocks, avoiding the overhead of the extract call is helpful
                    var str = length < 16 ? ShortStringInJs(length ?? 0) : LongStringInJs(length ?? 0);
                    if (str != null)
                        return str;
                }
                return ReadStringJs(length ?? 0);
            }
            else
            {
                object value;
                long length;
                switch (token)
                {
                    case 0xc0:
                        return null;
                    case 0xc1:
                        if (bundledStrings != null)
                        {
                            int start;
                            int len = Convert.ToInt32(Read()); // followed by the length of the string in characters (not bytes!)
                            if (len > 0)
                            {
                                start = bundledStrings.Position1;
                                bundledStrings.Position1 += len;
                                return bundledStrings.Strings[1].Substring(start, len);
                            }
                            else
                            {
                                start = bundledStrings.Position0;
                                bundledStrings.Position0 -= len;
                                return bundledStrings.Strings[0].Substring(start, Math.Abs(len));
                            }

                        }
                        return C1; // "never-used", return special object to denote that
                    case 0xc2:
                        return false;
                    case 0xc3:
                        return true;
                
                    case 0xc4:
                        // bin 8
                        value = ReadByte(position++);
                        if (value == null)
                            throw new Exception("Unexpected end of buffer");
                        return ReadBin((int)value);
                    case 0xc5:
                        // bin 16
                        length = GetUint16(position);
                        position += 2;
                        return ReadBin((int)length);
                    case 0xc6:
                        // bin 32
                        length = GetUint32(position);
                        position += 4;
                        return ReadBin((int)length);
                    case 0xc7:
                        // ext 8
                        return ReadExt(ReadByte(position++));
                    case 0xc8:
                        // ext 16
                        length = GetUint16(position);
                        position += 2;
                        return ReadExt((int)length);
                    case 0xc9:
                        // ext 32
                        length = GetUint32(position);
                        position += 4;
                        return ReadExt((int)length);
                    case 0xca:
                        value = GetFloat32(position);
                        if ((int)(currentUnpackr?.UseFloat32 ?? 0) > 2) {
                            // this does rounding of numbers that were encoded in 32-bit float to nearest significant decimal digit that could be preserved
                            var multiplier = float.Parse(Mult10[((ReadByte(position) & 0x7f) << 1) | (ReadByte(position+1) >> 7)]);
                            position += 4;
                            return ((multiplier * (float)value + ((float)value > 0 ? 0.5 : -0.5))) / multiplier;
                        }
                        position += 4;
                        return value;
                    case 0xcb:
                        value = GetFloat64(position);
                        position += 8;
                        return value;
                    // uint handlers
                    case 0xcc:
                        return ReadByte(position++);
                    case 0xcd:
                        value = GetUint16(position);
                        position += 2;
                        return value;
                    case 0xce:
                        value = GetUint32(position);
                        position += 4;
                        return value;
                    case 0xcf:
                        if (currentUnpackr?.Int64AsType == "number")
                        {
                            value = GetUint32(position) * 0x100000000;
                            value = (uint)value + GetUint32(position + 4);
                        } else if (currentUnpackr?.Int64AsType == "string")
                        {
                            value = GetBigUint64(position).ToString();
                        } else if (currentUnpackr?.Int64AsType == "auto")
                        {
                            value = GetBigUint64(position);
                            if ((BigInteger)value <= (BigInteger)2 << 52) 
                                value = (double)value;
                        }
                        else
                            value = GetBigUint64(position);
                        position += 8;
                        return value;
                    // int handlers
                    case 0xd0:
                        return GetInt8(position++);
                    case 0xd1:
                        value = GetInt16(position);
                        position += 2;
                        return value;
                    case 0xd2:
                        value = GetInt32(position);
                        position += 4;
                        return value;
                    case 0xd3:
                        if (currentUnpackr?.Int64AsType == "number")
                        {
                            value = GetInt32(position) * 0x100000000;
                            value = (long)value + GetUint32(position + 4);
                        } else if (currentUnpackr?.Int64AsType == "string")
                        {
                            value = GetBigInt64(position).ToString();
                        } else if (currentUnpackr?.Int64AsType == "auto")
                        {
                            value = GetBigInt64(position);
                            if (((long)value >= (long)-2 << 52) && (ulong)value <= (ulong)2 << 52) 
                                value = (decimal)value;
                        }
                        else
                            value = GetBigInt64(position);

                        position += 8;
                        return value;
                    case 0xd4:
                        // fixext 1
                        value = ReadByte(position++);
                        if ((byte)value == 0x72)
                        {
                            return RecordDefinition(ReadByte(position++));
                        }
                        else
                        {
                            if (_currentExtensions.ContainsKey((byte)value))
                            {
                                var extension = _currentExtensions[(byte)value];
                                if (extension.Read != null)
                                {
                                    position++; // skip filler byte
                                    return extension.Read(new[] { Read() });
                                }
                                else
                                {
                                    if (extension.Unpack != null)
                                    {
                                        if (extension.NoBuffer)
                                        {
                                            position++; // skip filler byte
                                            return extension.Unpack(null);
                                        }
                                    
                                        return extension.Unpack(ReadBytes(position++, 2));
                                    }
                                }
                            }
                            else
                                throw new NotImplementedException("Unknown extension " + value);
                        }
                        break;
                    case 0xd5:
                        // fixext 2
                        token = ReadByte(position);
                        if (token == 0x72)
                        {
                            position++;
                            return RecordDefinition(ReadByte(position++) & 0x3f, ReadByte(position++));
                        }
                        else
                            return ReadExt(2);
                    case 0xd6:
                        // fixext 4
                        return ReadExt(4);
                    case 0xd7:
                        // fixext 8
                        return ReadExt(8);
                    case 0xd8:
                        // fixext 16
                        return ReadExt(16);
                    case 0xd9:
                        // str 8
                        length = ReadByte(position++);
                        if (srcStringEnd >= position)
                        {
                            //return srcString.slice(position - srcStringStart, (position += value) - srcStringStart);
                            throw new NotImplementedException();
                        }
                        return ReadStringJs(length);
                    case 0xda:
                        // str 16
                        length = GetUint16(position);
                        position += 2;
                        if (srcStringEnd >= position)
                        {
                            //return srcString.slice(position - srcStringStart, (position += value) - srcStringStart);
                            throw new NotImplementedException();
                        }
                        return ReadStringJs(length);
                    case 0xdb:
                        // str 32
                        length = GetUint32(position);
                        position += 4;
                        if (srcStringEnd >= position)
                        {
                            //return srcString.slice(position - srcStringStart, (position += value) - srcStringStart);
                            throw new NotImplementedException();
                        }

                        return ReadStringJs(length);
                    case 0xdc:
                        // array 16
                        length = GetUint16(position);
                        position += 2;
                        return ReadArray(length);
                    case 0xdd:
                        // array 32
                        length = GetUint32(position);
                        position += 4;
                        return ReadArray(length);
                    case 0xde:
                        // map 16
                        length = GetUint16(position);
                        position += 2;
                        return ReadMap(length);
                    case 0xdf:
                        // map 32
                        length = GetUint32(position);
                        position += 4;
                        return ReadMap(length);
                    default: // negative int
                        if (token >= 0xe0)
                            return token - 0x100;
                        if (token == null)
                        {
                            var error = new ArgumentOutOfRangeException(nameof(token), "Unexpected end of MessagePack data");
                            //error.incomplete = true;
                            throw error;
                        }
                        throw new NotImplementedException("Unknown MessagePack token " + token);
                }
            }
            throw new NotImplementedException("Unknown MessagePack token " + token);
        }

        protected virtual MsgPackrStructureReadCallback CreateStructureReader(MsgPackrStructure structure, int? firstId = null)
        {


            // Todo: Implement second byte reader
            if (structure.HighByte == 0)
            {
                return CreateSecondByteReader(firstId, structure.GenericStructureReadCallback);
                throw new NotImplementedException();
            } 
            return structure.GenericStructureReadCallback;
        }

    
        public virtual MsgPackrStructureReadCallback CreateSecondByteReader (int? firstId, MsgPackrStructureReadCallback read0) {
            /*
            return function() {
            let highByte = src[position++]
            if (highByte === 0)
                return read0()
            let id = firstId < 32 ? -(firstId + (highByte << 5)) : firstId + (highByte << 5)
            let structure = currentStructures[id] || loadStructures()[id]
            if (!structure) {
                throw new Error('Record id is not defined for ' + id)
            }
            if (!structure.read)
                structure.read = createStructureReader(structure, firstId)
            return structure.read()
            }
            */
            return (object[] args) =>
            {
                var highByte = ReadByte(position++);
                if (highByte == 0)
                {
                    return read0(null);
                }

                var id = firstId < 32 ? -(firstId + (highByte << 5)) : firstId + (highByte << 5);
                MsgPackrStructure structure = null;
                if (id != null)
                {
                    structure = _currentStructures[id ?? 0] ?? LoadStructures()[id ?? 0];
                }

                if (structure == null)
                {
                    throw new Exception("Record id is not defined for " + id);
                }

                if (structure.Read == null)
                    structure.AddReadHandler(CreateStructureReader(structure, firstId));
                else
                {
                    return structure.Read();
                }

                return null;
            };
        }



        public MsgPackrStructure[] LoadStructures()
        {
            // Todo: Implement the following:
            /*
            var loadedStructures = SaveState(() => {
                // save the state in case getStructures modifies our buffer
                src = null;
                return currentUnpackr?.GetStructures();
            });
            return _currentStructures = currentUnpackr._mergeStructures(loadedStructures, _currentStructures);
            */
            throw new NotImplementedException();
        }

        public string ReadStringJs(long length)
        {
            string? result = null;

            if (length < 16)
            {
                if (!string.IsNullOrEmpty(result = ShortStringInJs((int)length)))
                    return result;
            }
            /*
            if (length > 64 && decoder)
                return decoder.decode(src.subarray(position$1, position$1 += length))
            */
            var end = position + length;
            var units = new StringBuilder();
            result = "";
            while (position < end)
            {
                var byte1 = ReadByte(position++);
                if ((byte1 & 0x80) == 0)
                {
                    // 1 byte
                    units.Append((char)byte1);
                }
                else if ((byte1 & 0xe0) == 0xc0)
                {
                    // 2 bytes
                    var byte2 = ReadByte(position++) & 0x3f;
                    units.Append((char)(((byte1 & 0x1f) << 6) | byte2));
                }
                else if ((byte1 & 0xf0) == 0xe0)
                {
                    // 3 bytes
                    var byte2 = ReadByte(position++) & 0x3f;
                    var byte3 = ReadByte(position++) & 0x3f;
                    units.Append((char)(((byte1 & 0x1f) << 12) | (byte2 << 6) | byte3));
                }
                else if ((byte1 & 0xf8) == 0xf0)
                {
                    // 4 bytes
                    var byte2 = ReadByte(position++) & 0x3f;
                    var byte3 = ReadByte(position++) & 0x3f;
                    var byte4 = ReadByte(position++) & 0x3f;
                    var unit = ((byte1 & 0x07) << 0x12) | (byte2 << 0x0c) | (byte3 << 0x06) | byte4;
                    if (unit > 0xffff)
                    {
                        unit -= 0x10000;
                        units.Append((char)(((unit >> 10) & 0x3ff) | 0xd800));
                        unit = 0xdc00 | (unit & 0x3ff);
                    }
                    units.Append((char)unit);
                }
                else
                {
                    units.Append((char)byte1);
                }
            }

            if (units.Length > 0)
            {
                return units.ToString();
            }

            return result;
        }

        protected IEnumerable<object?> ReadArray(long length)
        {
            // Todo: Implement the following:
            var array = new object?[length];
            for (var i = 0; i < length; i++) {
                array[i] = Read();
            }
            //if (currentUnpackr.FreezeData)
            //	return Object.freeze(array)
            return array;
        }

        protected static bool IsWholeValue(object value)
        {
            if (value is decimal decimalValue)
            {
                int precision = (Decimal.GetBits(decimalValue)[3] >> 16) & 0x000000FF;
                return precision == 0;
            }
            else if (value is float floatValue)
            {
                return floatValue == Math.Truncate(floatValue);
            }
            else if (value is double doubleValue)
            {
                return doubleValue == Math.Truncate(doubleValue);
            }

            return false;
        }

        public JToken GetJToken(object? value)
        {
            if (value == null)
                return null;
            else if (value.GetType().IsValueType || value is string)
                if ((value is decimal || value is float || value is double) && IsWholeValue(value))
                    return new JValue(Convert.ToInt64(value));
                else
                {
                    return new JValue(value);
                }
            else if (value is JToken token)
                return token;
            else if (value.GetType().IsArray)
            {
                // Have to retrieve each individual value
                // so each one is parsed correctly
                var jA = new JArray();
                var array = value as object[];
                for (int i = 0; i < array.Length; i++)
                {
                    jA.Add(GetJToken(array[i]));
                }

                return jA;
                //return JArray.FromObject(value);
            }
            else
                return JObject.FromObject(value);

            return null;
        }


        protected JToken ReadMap(long length)
        {
            if (currentUnpackr?.MapsAsObjects ?? false) {
                JObject obj = new JObject();
                for (var i = 0; i < length; i++) {
                    string key = ReadKey() ?? "";
                    if (key == "__proto__")
                        key = "__proto_";
                    object? value = Read();
                    obj[key] = GetJToken(value);
                }
                return obj;
            } else {
                JObject map = new JObject();
                for (var i = 0; i < length; i++) {
                    map[Read()?.ToString() ?? ""] = GetJToken(Read() ?? new {});
                }
                return map;
            }       
        }

        protected string FromCharCode(byte[] bytes, int length = 1) => Encoding.ASCII.GetString(bytes, 0, length);

        #region String Readers
        protected string? LongStringInJs(int length)
        {
            var start = position;
            var bytes = ReadBytes(position, length);
            for (var i = 0; i < length; i++)
            {
                var b = bytes[i];
                position++;
                if ((b & 0x80) > 0)
                {
                    position = start;
                    return null;
                }
            }
            return FromCharCode(bytes, length); ;
        }

        protected string? ShortStringInJs(int length)
        {
            var bytes = ReadBytes(position, length);
            if (length < 4)
            {
                if (length < 2)
                {
                    if (length == 0)
                        return "";
                    else
                    {
                        position++;
                        if ((bytes[0] & 0x80) > 1)
                        {
                            position -= 1;
                            return null;
                        }
                        return FromCharCode(bytes,1);
                    }
                }
                else
                {
                    position += 2;
                    if ((bytes[0] & 0x80) > 0 || (bytes[1] & 0x80) > 0)
                    {
                        position -= 2;
                        return null;
                    }
                    if (length < 3)
                        return FromCharCode(bytes,2);
                    position++;
                    if ((bytes[2] & 0x80) > 0)
                    {
                        position -= 3;
                        return null;
                    }
                    return FromCharCode(bytes,3);
                }
            }
            else
            {
                position += 4;
                if ((bytes[0] & 0x80) > 0 || (bytes[1] & 0x80) > 0 || (bytes[2] & 0x80) > 0 || (bytes[3] & 0x80) > 0)
                {
                    position -= 4;
                    return null;
                }
                if (length < 6)
                {
                    if (length == 4)
                        return FromCharCode(bytes, 4);
                    else
                    {
                        position++;
                        if ((bytes[4] & 0x80) > 0)
                        {
                            position -= 5;
                            return null;
                        }
                        return FromCharCode(bytes, 5);
                    }
                }
                else if (length < 8)
                {
                    position += 2;
                    if ((bytes[4] & 0x80) > 0 || (bytes[5] & 0x80) > 0)
                    {
                        position -= 6;
                        return null;
                    }
                    if (length < 7)
                        return FromCharCode(bytes, 6);
                    position++;
                    if ((bytes[6] & 0x80) > 0)
                    {
                        position -= 7;
                        return null;
                    }
                    return FromCharCode(bytes, 7);
                }
                else
                {
                    position += 4;
                    if ((bytes[4] & 0x80) > 0 || (bytes[5] & 0x80) > 0 || (bytes[6] & 0x80) > 0 || (bytes[7] & 0x80) > 0)
                    {
                        position -= 8;
                        return null;
                    }
                    if (length < 10)
                    {
                        if (length == 8)
                            return FromCharCode(bytes, 8);
                        else
                        {
                            position++;
                            if ((bytes[8] & 0x80) > 0)
                            {
                                position -= 9;
                                return null;
                            }
                            return FromCharCode(bytes, 9);
                        }
                    }
                    else if (length < 12)
                    {
                        position += 2;
                        if ((bytes[8] & 0x80) > 0 || (bytes[9] & 0x80) > 0)
                        {
                            position -= 10;
                            return null;
                        }
                        if (length < 11)
                            return FromCharCode(bytes, 10);
                        position++;
                        if ((bytes[10] & 0x80) > 0)
                        {
                            position -= 11;
                            return null;
                        }
                        return FromCharCode(bytes, 11);
                    }
                    else
                    {
                        position += 4;
                        if ((bytes[8] & 0x80) > 0 || (bytes[9] & 0x80) > 0 || (bytes[10] & 0x80) > 0 || (bytes[11] & 0x80) > 0)
                        {
                            position -= 12;
                            return null;
                        }
                        if (length < 14)
                        {
                            if (length == 12)
                                return FromCharCode(bytes, 12);
                            else
                            {
                                position++;
                                if ((bytes[12] & 0x80) > 0)
                                {
                                    position -= 13;
                                    return null;
                                }
                                return FromCharCode(bytes, 13);
                            }
                        }
                        else
                        {
                            position += 2;
                            if ((bytes[12] & 0x80) > 0 || (bytes[13] & 0x80) > 0)
                            {
                                position -= 14;
                                return null;
                            }
                            if (length < 15)
                                return FromCharCode(bytes, 14);
                            position++;
                            if ((bytes[14] & 0x80) > 0)
                            {
                                position -= 15;
                                return null;
                            }
                            return FromCharCode(bytes, 15);
                        }
                    }
                }
            }
            return null;
        }

        protected string? ReadOnlyJsString()
        {
            var token = ReadByte(position++);
            long length = 0;
            byte[] bytes;
            if (token < 0xc0)
            {
                // fixstr
                length = token - 0xa0;
            }
            else
            {
                switch (token)
                {
                    case 0xd9:
                        length = ReadByte(position++);
                        break;
                    case 0xda:
                        // str 16
                        length = GetUint16(position);
                        position += 2;
                        break;
                    case 0xdb:
                        // str 32
                        length = GetUint32(position);
                        position += 4;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(token), "Expected string");
                }
            }
            return ReadStringJs(length);
        }
        #endregion

        #region Javascript DataView readers
        public sbyte GetInt8(int offset)
        {
            return (sbyte)ReadByte(offset);
        }


        public short GetInt16(int offset)
        {
            byte[] bytes = ReadBytes(offset, 2);
            short b1 = (short)(bytes[0] << 8);
            short b2 = bytes[1];
            return (short)(b1 + b2);
        }

        public int GetInt32(int offset)
        {
            byte[] bytes = ReadBytes(offset, 4);
            return (bytes[0] << 24) + (bytes[1] << 16) + (bytes[2] << 8) + bytes[3];
        }

        public Int64 GetBigInt64(int offset)
        {
            byte[] bytes = ReadBytes(offset, 8);
            Int64 b1 = (Int64)bytes[0] << 56;
            Int64 b2 = (Int64)bytes[1] << 48;
            Int64 b3 = (Int64)bytes[2] << 40;
            Int64 b4 = (Int64)bytes[3] << 32;
            Int64 b5 = (Int64)bytes[4] << 24;
            Int64 b6 = (Int64)bytes[5] << 16;
            Int64 b7 = (Int64)bytes[6] << 8;
            Int64 b8 = (Int64)bytes[7];

            return (b1 + b2 + b3 + b4 + b5 + b6 + b7 + b8);
        }

        public UInt64 GetBigUint64(int offset)
        {
            byte[] bytes = ReadBytes(offset, 8);
            UInt64 b1 = (UInt64)bytes[0] << 56;
            UInt64 b2 = (UInt64)bytes[1] << 48;
            UInt64 b3 = (UInt64)bytes[2] << 40;
            UInt64 b4 = (UInt64)bytes[3] << 32;
            UInt64 b5 = (UInt64)bytes[4] << 24;
            UInt64 b6 = (UInt64)bytes[5] << 16;
            UInt64 b7 = (UInt64)bytes[6] << 8;
            UInt64 b8 = (UInt64)bytes[7];

            return (b1 + b2 + b3 + b4 + b5 + b6 + b7 + b8);
        }


        public ushort GetUint16(int offset)
        {
            byte[] bytes = ReadBytes(offset, 2);
            ushort b1 = (ushort)(bytes[0] << 8);
            ushort b2 = bytes[1];
            return (ushort)(b1 + b2);
        }

        public uint GetUint32(int offset)
        {
            byte[] bytes = ReadBytes(offset, 4);
            return ((uint)bytes[0] << 24) + ((uint)bytes[1] << 16) + ((uint)bytes[2] << 8) + (uint)bytes[3];
        }

        public float GetFloat32(int offset)
        {
            int n = GetInt32(offset);
            return BitConverter.Int32BitsToSingle(n);
        }

        public double GetFloat64(int offset)
        {
            long n = GetBigInt64(offset);
            return BitConverter.Int64BitsToDouble(n);
        }
        #endregion

        protected byte[] ReadBin(int length)
        {
            // Todo: Implement the following:
            /*
            return currentUnpackr.CopyBuffers
                ?
                // specifically use the copying slice (not the node one)
                Uint8Array.prototype.slice.call(src, position, position += length)
                : src.subarray(position, position += length);
            */
            throw new NotImplementedException();
        }

        protected object? ReadExt(int length)
        {
            var type = ReadByte(position++);

            if (_currentExtensions.TryGetValue(type, out var extension))
            {
                int end = 0;
                /*
                let end;
                   return currentExtensions[type](src.subarray(position$1, end = (position$1 += length)), (readPosition) => {
                    position$1 = readPosition;
                    try {
                        return read();
                    } finally {
                        position$1 = end;
                    }
                   })
                */
                var unpack = extension.Unpack;
                if (unpack != null)
                {
                    var n = position;
                    position += length;
                    end = position;
                    object? value = null;
                    try
                    {
                        value = unpack.Invoke(ReadBytes(n, length));
                    }
                    finally
                    {
                        //position = end;
                    }
                    return value;
                }
            }
            throw new NotImplementedException($"Extension not implemented: {type}");
        }

        protected string? ReadKey()
        {
            // Todo: Implement the following:
            long length = ReadByte(position++);
            if (length >= 0xa0 && length < 0xc0) 
            {
                // fixstr, potentially use key cache
                length = length - 0xa0;
                if (srcStringEnd >= position) // if it has been extracted, must use it (and faster anyway)
                    //return srcString.Slice(position - srcStringStart, (position += length) - srcStringStart);
                    throw new NotImplementedException();
                else if (!(srcStringEnd == 0 && srcEnd < 180))
                    return ReadStringJs(length);
            } 
            else 
            { // not cacheable, go back and do a standard read
                position--;
                return AsSafeString(Read());
            }
            var key = ((length << 5) ^ (length > 1 ? GetUint16(position) : length > 0 ? ReadByte(position) : 0)) & 0xfff;
            MsgPackrKeyCacheEntry? entry = KeyCache[(int)key];
            var checkPosition = position;
            var end = position + length - 3;
            long chunk;
            var i = 0;
            if (entry != null && entry.Bytes == length) 
            {
                while (checkPosition < end) {
                    chunk = GetUint32(checkPosition);
                    if (chunk != entry[i++]) {
                        checkPosition = 0x70000000;
                        break;
                    }
                    checkPosition += 4;
                }
                end += 3;
                while (checkPosition < end) {
                    chunk = ReadByte(checkPosition++);
                    if (chunk != entry[i++]) {
                        checkPosition = 0x70000000;
                        break;
                    }
                }
                if (checkPosition == end) {
                    position = checkPosition;
                    return entry.String;
                }
                end -= 3;
                checkPosition = position;
            }
            entry = new MsgPackrKeyCacheEntry();
            KeyCache[key] = entry;
            entry.Bytes = length;
            while (checkPosition < end) {
                chunk = GetUint32(checkPosition);
                entry.Add(chunk);
                checkPosition += 4;
            }
            end += 3;
            while (checkPosition < end) {
                chunk = ReadByte(checkPosition++);
                entry.Add(chunk);
            } 
            // for small blocks, avoiding the overhead of the extract call is helpful
            var str = length < 16 ? ShortStringInJs((int)length) : LongStringInJs((int)length);
            if (str != null) 
                return entry.String = str;
            return entry.String = ReadStringJs(length);        
        }

        protected string AsSafeString(object? property)
        {
            if (property is string)
            {
                return (string)property;
            }
            if (property?.GetType().IsPrimitive ?? false)
            {
                return property?.ToString() ?? "";
            }
            throw new ArrayTypeMismatchException("Invalid property type for record: " + property?.GetType().Name);
        }

        protected virtual object? RecordDefinition(int id, int? highByte = null)
        {
            MsgPackrStructure? structure = null;
            try
            {
                // ensure that all keys are strings and
                // that the array is mutable
                var obj = Read();
                var data = ((IEnumerable<object?>?)obj)?.ToArray() ?? new object?[]{};
            
                for (var i = 0; i < data.Count(); i++)
                {
                    data[i] = AsSafeString(data[i]);
                } 
                structure = Map(data);
            }
            catch
            {
                throw new ArgumentOutOfRangeException(nameof(structure), $"Unable to map structure: {id}:{structure}");
            }
            var firstByte = id;
            if (highByte != null) 
            { 
                id = id < 32 ? -(((highByte ?? 0) << 5) + id) : (((highByte ?? 0) << 5) + id); 
                structure.HighByte = highByte;
            }
            var existingStructure = _currentStructures.FirstOrDefault(o => o.Key == id).Value;
            // If it is a shared structure, we need to restore any changes after reading.
            // Also in sequential mode, we may get incomplete reads and thus errors, and we need to restore
            // to the state prior to an incomplete read in order to properly resume.
            // Todo: Implement the following: 
        
            if (existingStructure != null && (existingStructure.IsShared || sequentialMode)) 
            {
                (_currentStructures.RestoreStructures ?? (_currentStructures.RestoreStructures = new Dictionary<int, MsgPackrStructure>()))[id] = existingStructure;
                //throw new InvalidOperationException();
            }
        
            _currentStructures[id] = structure;
            structure.AddReadHandler(CreateStructureReader(structure, firstByte));
            return structure.Read?.Invoke();
        }

        protected object SaveState(object callback = null)
        {
            // Todo: Implement the following:
            /*
            let savedSrcEnd = srcEnd;
               let savedPosition = position$1;
               let savedSrcStringStart = srcStringStart;
               let savedSrcStringEnd = srcStringEnd;
               let savedSrcString = srcString;
               let savedReferenceMap = referenceMap;
               let savedBundledStrings = bundledStrings$1;

               // TODO: We may need to revisit this if we do more external calls to user code (since it could be slow)
               let savedSrc = new Uint8Array(src.slice(0, srcEnd)); // we copy the data in case it changes while external data is processed
               let savedStructures = currentStructures;
               let savedStructuresContents = currentStructures.slice(0, currentStructures.length);
               let savedPackr = currentUnpackr;
               let savedSequentialMode = sequentialMode;
               let value = callback();
               srcEnd = savedSrcEnd;
               position$1 = savedPosition;
               srcStringStart = savedSrcStringStart;
               srcStringEnd = savedSrcStringEnd;
               srcString = savedSrcString;
               referenceMap = savedReferenceMap;
               bundledStrings$1 = savedBundledStrings;
               src = savedSrc;
               sequentialMode = savedSequentialMode;
               currentStructures = savedStructures;
               currentStructures.splice(0, currentStructures.length, ...savedStructuresContents);
               currentUnpackr = savedPackr;
               dataView = new DataView(src.buffer, src.byteOffset, src.byteLength);
               return value
            */
            throw new NotImplementedException();
        }


        public void ClearSource()
        {
            src = null;
            referenceMap = null;
            _currentStructures.Clear();
        }

        public void AddExtension<T>() where T: MsgPackrExtension, new()
        {
            T ext = MsgPackrExtension.GetNewInstance<T>(this);
            if (!_currentExtensions.ContainsKey(ext.Type))
                _currentExtensions[ext.Type] = ext;
        }







        public void Seek(int offset)
        {
            position = offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] ReadBytes(int offset, int length = 1) => src.Slice(offset, length).ToArray();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadByte(int offset) => src.Slice(offset, 1).ToArray()[0];

        protected MsgPackrStructure Map(object? obj)
        {

            if (obj is IEnumerable<object?> data)
            {
                IEnumerable<string?> keys = data.Select(o => o?.ToString());
                return MsgPackrStructure.GetNewInstance<MsgPackrStructure>(this, keys);
            }
            return MsgPackrStructure.GetNewInstance<MsgPackrStructure>(this, null);
        }

        /*
        private object? ReadExtension(byte token)
        {
            CborReaderState readerState = CborReaderState.Tag;
            JsonToken type = JsonToken.None;
            object? value = null;
            if (!_currentExtensions.ContainsKey((int)token))
                throw new NotImplementedException($"Extension not implemented: {type}");

            switch (token)
            {
                case 2:
                    break;
                case 20:
                    // Read
                    CborNode? node = ReadObject(); // Read token
                    return _currentExtensions[token](new object?[]{node?.Value});
                    break;
                case 214:
                    break;
                default:
                    throw new NotImplementedException($"Extension not implemented: {token}");
            }

            return new CborNode
            {
                State = readerState,
                NodeType = type,
                Value = value
            };
        }
        */

        protected virtual object? ReadStructure(int id, object? structure)
        {
            throw new NotImplementedException("Structure is not implemented");
        }


    }
}