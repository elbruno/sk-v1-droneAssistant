using Newtonsoft.Json;
using System.Text;

namespace TelloSharp
{
    public class TelloLog
    {
        public class FieldSpec
        {
            public string? Name { get; set; }
            public short Offset { get; set; }
            public string? Type { get; set; }
            public object? Value { get; set; }
        }

        public class RecordSpec
        {
            public string? DefinedIn { get; set; }
            public string? Name { get; set; }
            public ushort Id { get; set; }
            public ushort Len { get; set; }
            public List<FieldSpec>? Fields { get; set; }

            public RecordSpec()
            {
                Fields = new List<FieldSpec>();
            }

            public override string ToString()
            {
                string? str = string.Empty;
                foreach (FieldSpec? field in Fields)
                {
                    str += string.Format("{0}.{1} = {2}\n", Name, field.Name, field.Value);
                }
                return str;
            }
        }

        private readonly RecordSpec[] recordSpecs;
        private readonly Dictionary<string, RecordSpec> recordSpecLookup;
        private readonly Dictionary<string, FieldSpec> fieldSpecLookup;

        /// <summary>
        /// TelloLog
        /// </summary>
        public TelloLog()
        {
            string? srcPath = "./";
            string? json = File.ReadAllText(srcPath + "parsedRecSpecs.json");
            recordSpecs = JsonConvert.DeserializeObject(json, typeof(RecordSpec[])) as RecordSpec[];


            if (recordSpecs != null)
            {
                recordSpecLookup = new();
                fieldSpecLookup = new();

                foreach (RecordSpec? r in recordSpecs)
                {
                    recordSpecLookup[r.Id.ToString()] = r;
                    if (r.Id == ushort.MaxValue)
                    {

                    }
                    if (r.Fields != null)
                    {
                        foreach (FieldSpec? f in r.Fields)
                        {
                            fieldSpecLookup[r.Name + "." + f.Name] = f;
                        }
                    }
                }
            }

        }

        public RecordSpec[] Parse(byte[] data)
        {
            List<RecordSpec>? records = new List<RecordSpec>();
            ushort pos = 0;

            while (pos < data.Length - 2)//-2 for CRC bytes at end of packet.
            {
                if (data[pos] != 'U')//Check magic byte
                {
                    pos += 1;
                    Console.WriteLine("PARSE ERROR!!!");
                    continue;
                }
                byte len = data[pos + 1];
                if (data[pos + 2] != 0)//Should always be zero (so far)
                {
                    pos += 1;
                    Console.WriteLine("SIZE OVERFLOW!!!");
                    break;
                }
                byte crc = data[pos + 3];
                //todo Check crc.

                ushort id = BitConverter.ToUInt16(data, pos + 4);
                byte[]? xorBuf = new byte[256];
                byte xorValue = data[pos + 6];


                string? recSpecId = id.ToString();
                Console.WriteLine(recSpecId);
                if (recordSpecLookup.Keys.Contains(recSpecId))
                {
                    for (int i = 0; i < len; i++)//Decrypt payload.
                    {
                        xorBuf[i] = (byte)(data[pos + i] ^ xorValue);
                    }

                    int baseOffset = 10;
                    RecordSpec? record = recordSpecLookup[recSpecId];

                    RecordSpec? newRecord = new RecordSpec()
                    {
                        Name = record.Name,
                        Id = record.Id,
                        DefinedIn = record.DefinedIn,
                        Len = record.Len,
                        Fields = new List<FieldSpec>()
                    };

                    List<FieldSpec>? fields = record.Fields;
                    foreach (FieldSpec? field in fields)
                    {
                        switch (field.Type)
                        {
                            case "byte":
                                field.Value = xorBuf[baseOffset + field.Offset];
                                break;
                            case "short":
                                field.Value = BitConverter.ToInt16(xorBuf, baseOffset + field.Offset);
                                break;
                            case "UInt16":
                                field.Value = BitConverter.ToUInt16(xorBuf, baseOffset + field.Offset);
                                break;
                            case "int":
                                field.Value = BitConverter.ToInt32(xorBuf, baseOffset + field.Offset);
                                break;
                            case "UInt32":
                                field.Value = BitConverter.ToUInt32(xorBuf, baseOffset + field.Offset);
                                break;
                            case "float":
                                field.Value = BitConverter.ToSingle(xorBuf, baseOffset + field.Offset);
                                break;
                            case "double":
                                field.Value = BitConverter.ToDouble(xorBuf, baseOffset + field.Offset);
                                break;
                            case "string":
                                field.Value = Encoding.Default.GetString(xorBuf, baseOffset + field.Offset, len - 15);
                                break;
                        }
                        FieldSpec? newField = new FieldSpec()
                        {
                            Name = field.Name,
                            Type = field.Type,
                            Offset = field.Offset,
                        };
                        newField.Value = field.Value;
                        newRecord.Fields.Add(newField);
                    }

                    Console.WriteLine(record.ToString());

                    records.Add(newRecord);
                }
                else
                {
                    Console.WriteLine("Not found:" + recSpecId + " len:" + len);
                }
                pos += len;
            }
            return records.ToArray();
        }
    }
}

