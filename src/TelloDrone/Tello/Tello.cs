using System.Text;
using static TelloSharp.Messages;

namespace TelloSharp
{
    public class Tello
    {
        private UdpUser _client;
        private FlyData _state;
        private FileInternal fileTemp;
        private DateTime lastMessageTime;//for connection timeouts.
        
        internal byte _wifiStrength = 0;
        internal short _controlSequence = 1;
        internal bool _connected = false;

        public event EventHandler<TelloStateEventArgs> OnUpdate;
        public event EventHandler<ConnectionState> OnConnection;

        internal string picPath;      //todo redo this. 
        internal string picFilePath;  //todo redo this. 
        internal int picMode = 0;     //pic or vid aspect ratio.
              

        public enum ConnectionState
        {
            Disconnected,
            Connecting,
            Connected,
            Paused,
            UnPausing
        }

        public ConnectionState _connectionState = ConnectionState.Disconnected;
        private CancellationTokenSource _cancelTokens = new CancellationTokenSource();
        public CancellationTokenSource CancelTokens { get => _cancelTokens; set => _cancelTokens = value; }

        public FlyData State => _state;

        public Tello()
        {
            _state = new FlyData(this);
            _client = new UdpUser();
        }

        public void TakeOff()
        {
            var pkt = NewPacketAsBytes(PacketType.ptSet, MessageTypes.msgDoTakeoff, _controlSequence++, 0);
            _client.Send(pkt);
        }

        public void StopSmartVideo(SmartVideoCmd cmd)
        {
            var pkt = NewPacket(PacketType.ptSet, MessageTypes.msgDoSmartVideo, _controlSequence++, 1);
            pkt.payload[0] = (byte)cmd;
            var buffer = PacketToBuffer(pkt);
            _client.Send(buffer);
        }

        public void Hover()
        {
            ctrlRx = 0;
            ctrlRy = 0;
            ctrlLx = 0;
            ctrlLy = 0;
            SendStickUpdate();
        }

        public void SendCommand(string cmd)
        {            
            _client.Send(Encoding.UTF8.GetBytes(cmd));
        }

        /// <summary>
        /// StartSmartVideo
        /// </summary>
        /// <param name="cmd"></param>
        public void StartSmartVideo(SmartVideoCmd cmd)
        {
            var pkt = NewPacket(PacketType.ptSet, MessageTypes.msgDoSmartVideo, _controlSequence++, 1);
            pkt.payload[0] = (byte)((byte)cmd | 0x01);
            var buffer = PacketToBuffer(pkt);
            _client.Send(buffer);
        }

        public void ThrowTakeOff()
        {
            var pkt = NewPacketAsBytes(PacketType.ptGet, MessageTypes.msgDoThrowTakeoff, _controlSequence++, 0);
            _client.Send(pkt);
        }

        public void Land()
        {
            var pkt = NewPacket(PacketType.ptSet, MessageTypes.msgDoLand, _controlSequence++, 1);
            pkt.payload[0] = 0; // see StopLanding() for use of this field
            var buffer = PacketToBuffer(pkt);
            _client.Send(buffer);
        }

        public void StopLanding()
        {
            var pkt = NewPacket(PacketType.ptSet, MessageTypes.msgDoLand, _controlSequence++, 1);
            pkt.payload[0] = 1;
            var buffer = PacketToBuffer(pkt);
            _client.Send(buffer);
        }

        public void PalmLanding()
        {
            var pkt = NewPacket(PacketType.ptSet, MessageTypes.msgDoPalmLand, _controlSequence++, 1);
            pkt.payload[0] = 0;
            var buffer = PacketToBuffer(pkt);
            _client.Send(buffer);
        }

        public void Bounce()
        {
            var pkt = NewPacket(PacketType.ptSet, MessageTypes.msgDoBounce, _controlSequence++, 1);

            if (ctrlBouncing)
            {
                pkt.payload[0] = 0x31;
                ctrlBouncing = false;
            }
            else
            {
                pkt.payload[0] = 0x30;
                ctrlBouncing = true;
            }

            var buffer = PacketToBuffer(pkt);
            _client.Send(buffer);
        }
        
        private int Int16ToTello(short sv)
        {
            // sv is in range -32768 to 32767, we need 660 to 1388 where 0 => 1024
            //return uint64((sv / 90) + 1024)
            // Changed this as new info (Oct 18) suggests range should be 364 to 1684...
            return (int)(sv / 49.672 + 1024);
        }

        private void UpdateSticks(StickMessage message)
        {
            ctrlRx = message.Rx;
            ctrlRy = message.Ry;
            ctrlLx = message.Lx;
            ctrlLy = message.Ly;
        }

        private void SendDateTime()
        {
            var pkt = new Packet
            {

                // populate the command packet fields we need
                header = msgHdr,
                toDrone = true,
                packetType = PacketType.ptData1,
                messageID = MessageTypes.msgSetDateTime,
                payload = new byte[11]
            };

            _controlSequence++;
            pkt.sequence = _controlSequence;

            pkt.payload = new byte[15];
            pkt.payload[0] = 0;

            var now = DateTime.Now;
            pkt.payload[1] = (byte)now.Year;
            pkt.payload[2] = (byte)((byte)now.Year >> 8);
            pkt.payload[3] = (byte)now.Month;
            pkt.payload[4] = (byte)((byte)now.Month >> 8);
            pkt.payload[5] = (byte)now.Day;
            pkt.payload[6] = (byte)((byte)now.Day >> 8);
            pkt.payload[7] = (byte)now.Hour;
            pkt.payload[8] = (byte)((byte)now.Hour >> 8);
            pkt.payload[9] = (byte)now.Minute;
            pkt.payload[10] = (byte)((byte)now.Minute >> 8);
            pkt.payload[11] = (byte)now.Second;
            pkt.payload[12] = (byte)((byte)now.Second >> 8);            
            pkt.payload[13] = (byte)now.Millisecond;
            pkt.payload[14] = (byte)((byte)now.Millisecond >> 8);

            var buffer = PacketToBuffer(pkt);
            _client.Send(buffer);
        }

        public void Forward(int pct)
        {
            short speed = 0;

            if (pct > 0)
            {
                speed = (short)(pct * 327); // /100 * 32767
            }
            UpdateSticks(new StickMessage() { Rx = 0, Ry = speed, Lx = 0, Ly = 0 });
        }

        public void Backward(int pct)
        {
            short speed = 0;

            if (pct > 0)
            {
                speed = (short)(pct * 327); // /100 * 32767
            }
            UpdateSticks(new StickMessage() { Rx = 0, Ry = (short)-speed, Lx = 0, Ly = 0 });
        }

        public void Left(int pct)
        {
            short speed = 0;

            if (pct > 0)
            {
                speed = (short)(pct * 327); // /100 * 32767
            }
            UpdateSticks(new StickMessage() { Rx = (short)-speed, Ry = 0, Lx = 0, Ly = 0 });
        }

        public void Right(int pct)
        {
            short speed = 0;

            if (pct > 0)
            {
                speed = (short)(pct * 327); // /100 * 32767
            }
            UpdateSticks(new StickMessage() { Rx = speed, Ry = 0, Lx = 0, Ly = 0 });
        }

        public void Up(int pct)
        {
            short speed = 0;

            if (pct > 0)
            {
                speed = (short)(pct * 327); // /100 * 32767
            }
            UpdateSticks(new StickMessage() { Rx = 0, Ry = 0, Lx = 0, Ly = speed });
        }

        public void Down(int pct)
        {
            short speed = 0;

            if (pct > 0)
            {
                speed = (short)(pct * 327); // /100 * 32767
            }
            UpdateSticks(new StickMessage() { Rx = 0, Ry = 0, Lx = 0, Ly = (short)-speed });
        }

        public void ClockWise(int pct)
        {
            short speed = 0;

            if (pct > 0)
            {
                speed = (short)(pct * 327); // /100 * 32767
            }
            UpdateSticks(new StickMessage() { Rx = 0, Ry = 0, Lx = speed, Ly = 0 });
        }

        public void AntiClockWise(int pct)
        {
            short speed = 0;

            if (pct > 0)
            {
                speed = (short)(pct * 327); // /100 * 32767
            }
            UpdateSticks(new StickMessage() { Rx = 0, Ry = 0, Lx = (short)-speed, Ly = 0 });
        }


        public void SendRCAxis(short rx, short ry, short lx, short ly)
        {
            if (rx > 0)
            {
                rx = (short)(rx * 327); // /100 * 32767
            }
            if (ry > 0)
            {
                ry = (short)(ry * 327); // /100 * 32767
            }
            if (lx > 0)
            {
                lx = (short)(lx * 327); // /100 * 32767
            }
            if (ly > 0)
            {
                ly = (short)(ly * 327); // /100 * 32767
            }

            UpdateSticks(new StickMessage() { Rx = rx, Ry = ry, Lx = lx, Ly = ly });
        }

        public void SetSportsMode(bool sports)
        {
            ctrlSportsMode = sports;
        }

        public void SetFastMode()
        {
            SetSportsMode(true);
        }

        public void SetSlowMode()
        {
            SetSportsMode(false);
        }

        public void GetLowBatteryThreshold()
        {
            var pkt = NewPacketAsBytes(PacketType.ptGet, MessageTypes.msgQueryLowBattThresh, _controlSequence++, 0);
            _client.Send(pkt);
        }

        public void GetMaxHeight()
        {
            var pkt = NewPacketAsBytes(PacketType.ptGet, MessageTypes.msgQueryHeightLimit, _controlSequence++, 0);
            _client.Send(pkt);
        }

        public void GetSSID()
        {
            var pkt = NewPacketAsBytes(PacketType.ptGet, MessageTypes.msgQuerySSID, _controlSequence++, 0);
            _client.Send(pkt);
        }

        public void GetVersion()
        {
            var pkt = NewPacketAsBytes(PacketType.ptGet, MessageTypes.msgQueryVersion, _controlSequence++, 0);
            _client.Send(pkt);
        }

        public void SetLowBatteryThreshold(byte thr)
        {
            var pkt = NewPacket(PacketType.ptSet, MessageTypes.msgSetLowBattThresh, _controlSequence++, 1);
            pkt.payload[0] = thr;
            _client.Send(PacketToBuffer(pkt));
        } 
 
        public void QueryUnk(int cmd)
        {
            byte[]? packet = new byte[] { 0xcc, 0x58, 0x00, 0x7c, 0x48, 0xff, 0x00, 0x06, 0x00, 0xe9, 0xb3 };
            packet[5] = (byte)cmd;
            SetPacketSequence(packet);
            SetPacketCRC(packet);
            _client.Send(packet);
        }

        public void QueryAttAngle()
        {
            byte[]? packet = new byte[] { 0xcc, 0x58, 0x00, 0x7c, 0x48, 0x59, 0x10, 0x06, 0x00, 0xe9, 0xb3 };
            SetPacketSequence(packet);
            SetPacketCRC(packet);
            _client.Send(packet);
        }

        public void QueryMaxHeight()
        {
            byte[]? packet = new byte[] { 0xcc, 0x58, 0x00, 0x7c, 0x48, 0x56, 0x10, 0x06, 0x00, 0xe9, 0xb3 };
            SetPacketSequence(packet);
            SetPacketCRC(packet);
            _client.Send(packet);
        }

        public void SetAttAngle(float angle)
        {
            //                                          crc    typ  cmdL  cmdH  seqL  seqH  ang1  ang2 ang3  ang4  crc   crc
            byte[]? packet = new byte[] { 0xcc, 0x78, 0x00, 0x27, 0x68, 0x58, 0x10, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x5b, 0xc5 };

            //payload
            byte[] bytes = BitConverter.GetBytes(angle);
            packet[9] = bytes[0];
            packet[10] = bytes[1];
            packet[11] = bytes[2];
            packet[12] = bytes[3];

            SetPacketSequence(packet);
            SetPacketCRC(packet);

            _client.Send(packet);

            QueryAttAngle();//refresh
        }

        public void SetEis(int value)
        {
            //                                          crc    typ  cmdL  cmdH  seqL  seqH  valL  crc   crc
            byte[]? packet = new byte[] { 0xcc, 0x60, 0x00, 0x27, 0x68, 0x24, 0x00, 0x09, 0x00, 0x00, 0x5b, 0xc5 };
            //payload
            packet[9] = (byte)(value & 0xff);

            SetPacketSequence(packet);
            SetPacketCRC(packet);

            _client.Send(packet);
        }

        public void Flip(FlipType dir)
        {
            var pkt = NewPacket(PacketType.ptFlip, MessageTypes.msgDoFlip, _controlSequence++, 1);
            pkt.payload[0] = (byte)dir;
            var buffer = PacketToBuffer(pkt);
            _client.Send(buffer);
        }

        public void SetJpgQuality(int quality)
        {
            //                                          crc    typ  cmdL  cmdH  seqL  seqH  quaL  crc   crc
            byte[]? packet = new byte[] { 0xcc, 0x60, 0x00, 0x27, 0x68, 0x37, 0x00, 0x09, 0x00, 0x00, 0x5b, 0xc5 };

            //payload
            packet[9] = (byte)(quality & 0xff);

            SetPacketSequence(packet);
            SetPacketCRC(packet);

            _client.Send(packet);
        }

        public void SetEV(int ev)
        {
            //                                          crc    typ  cmdL  cmdH  seqL  seqH  evL  crc   crc
            byte[]? packet = new byte[] { 0xcc, 0x60, 0x00, 0x27, 0x68, 0x34, 0x00, 0x09, 0x00, 0x00, 0x5b, 0xc5 };

            byte evb = (byte)(ev - 9);//Exposure goes from -9 to +9
                                      //payload
            packet[9] = evb;

            SetPacketSequence(packet);
            SetPacketCRC(packet);

            _client.Send(packet);
        }

        public void SetVideoBitRate(int rate)
        {
            //                                          crc    typ  cmdL  cmdH  seqL  seqH  rateL  crc   crc
            byte[]? packet = new byte[] { 0xcc, 0x60, 0x00, 0x27, 0x68, 0x20, 0x00, 0x09, 0x00, 0x00, 0x5b, 0xc5 };

            //payload
            packet[9] = (byte)rate;

            SetPacketSequence(packet);
            SetPacketCRC(packet);

            _client.Send(packet);
        }

        public void SetVideoDynRate(int rate)
        {
            //                                          crc    typ  cmdL  cmdH  seqL  seqH  rateL  crc   crc
            byte[]? packet = new byte[] { 0xcc, 0x60, 0x00, 0x27, 0x68, 0x21, 0x00, 0x09, 0x00, 0x00, 0x5b, 0xc5 };

            //payload
            packet[9] = (byte)rate;

            SetPacketSequence(packet);
            SetPacketCRC(packet);

            _client.Send(packet);
        }

        public void SetVideoRecord(int n)
        {
            //                                          crc    typ  cmdL  cmdH  seqL  seqH  nL  crc   crc
            byte[]? packet = new byte[] { 0xcc, 0x60, 0x00, 0x27, 0x68, 0x32, 0x00, 0x09, 0x00, 0x00, 0x5b, 0xc5 };

            //payload
            packet[9] = (byte)n;

            SetPacketSequence(packet);
            SetPacketCRC(packet);

            _client.Send(packet);
        }

        /*TELLO_CMD_SWITCH_PICTURE_VIDEO
        49 0x31
        0x68
        switching video stream mode
        data: u8 (1=video, 0=photo)
        */
        public void SetPicVidMode(int mode)
        {
            //                                          crc    typ  cmdL  cmdH  seqL  seqH  modL  crc   crc
            byte[]? packet = new byte[] { 0xcc, 0x60, 0x00, 0x27, 0x68, 0x31, 0x00, 0x00, 0x00, 0x00, 0x5b, 0xc5 };

            picMode = mode;

            //payload
            packet[9] = (byte)(mode & 0xff);

            SetPacketSequence(packet);
            SetPacketCRC(packet);

            _client.Send(packet);
        }

        public void TakePicture()
        {
            //                                         crc   typ  cmdL  cmdH  seqL  seqH  crc   crc
            byte[]? packet = new byte[] { 0xcc, 0x58, 0x00, 0x7c, 0x68, 0x30, 0x00, 0x06, 0x00, 0xe9, 0xb3 };
            SetPacketSequence(packet);
            SetPacketCRC(packet);
            _client.Send(packet);
            Console.WriteLine("PIC START");
        }

        public void SendAckFilePiece(byte endFlag, ushort fileId, uint pieceId)
        {
            //                                          crc    typ  cmdL  cmdH  seqL  seqH  byte  nL    nH    n2L                     crc   crc
            byte[]? packet = new byte[] { 0xcc, 0x90, 0x00, 0x27, 0x50, 0x63, 0x00, 0xf0, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x5b, 0xc5 };

            packet[9] = endFlag;
            packet[10] = (byte)(fileId & 0xff);
            packet[11] = (byte)((fileId >> 8) & 0xff);

            packet[12] = ((byte)(int)(0xFF & pieceId));
            packet[13] = ((byte)(int)(pieceId >> 8 & 0xFF));
            packet[14] = ((byte)(int)(pieceId >> 16 & 0xFF));
            packet[15] = ((byte)(int)(pieceId >> 24 & 0xFF));

            SetPacketSequence(packet);
            SetPacketCRC(packet);

            _client.Send(packet);
        }

        public void SendAckFileSize()
        {
            var pkt = NewPacketAsBytes(PacketType.ptData1, MessageTypes.msgFileSize, _controlSequence++, 1);
            _client.Send(pkt);            
        }

        public void SendAckFileDone(int size)
        {
            //                                          crc    typ  cmdL  cmdH  seqL  seqH  fidL  fidH  size  size  size  size  crc   crc
            byte[]? packet = new byte[] { 0xcc, 0x88, 0x00, 0x24, 0x48, 0x64, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x5b, 0xc5 };

            //packet[9] = (byte)(fileid & 0xff);
            //packet[10] = (byte)((fileid >> 8) & 0xff);

            packet[11] = ((byte)(0xFF & size));
            packet[12] = ((byte)(size >> 8 & 0xFF));
            packet[13] = ((byte)(size >> 16 & 0xFF));
            packet[14] = ((byte)(size >> 24 & 0xFF));
            SetPacketSequence(packet);
            SetPacketCRC(packet);

            _client.Send(packet);
        }

        public void SendAckLogHeader(byte[] id)
        {
            var pkt = NewPacket(PacketType.ptData1, MessageTypes.msgLogHeader, _controlSequence++, 3);
            pkt.payload[1] = id[0];
            pkt.payload[2] = id[1];
            _client.Send(PacketToBuffer(pkt));
        }

        public void SendAckLogHeader(short cmd, ushort id)
        {
            //                                          crc    typ  cmdL  cmdH  seqL  seqH  unk   idL   idH   crc   crc
            var packet = new byte[] { 0xcc, 0x70, 0x00, 0x27, 0x50, 0x50, 0x10, 0x00, 0x00, 0x00, 0x00, 0x00, 0x5b, 0xc5 };

            var ba = BitConverter.GetBytes(cmd);
            packet[5] = ba[0];
            packet[6] = ba[1];

            ba = BitConverter.GetBytes(id);
            packet[10] = ba[0];
            packet[11] = ba[1];
            SetPacketSequence(packet);
            SetPacketCRC(packet);
            _client.Send(packet);
        }

        //this might not be working right 
        public void SendAckLogConfig(short cmd, ushort id, int n2)
        {
            //                                          crc    typ  cmdL  cmdH  seqL  seqH  unk   idL   idH   n2L   n2H   n2L   n2H   crc   crc
            byte[]? packet = new byte[] { 0xcc, 0xd0, 0x00, 0x27, 0x88, 0x50, 0x10, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x5b, 0xc5 };

            byte[]? ba = BitConverter.GetBytes(cmd);
            packet[5] = ba[0];
            packet[6] = ba[1];

            ba = BitConverter.GetBytes(id);
            packet[10] = ba[0];
            packet[11] = ba[1];

            packet[12] = ((byte)(0xFF & n2));
            packet[13] = ((byte)(n2 >> 8 & 0xFF));
            packet[14] = ((byte)(n2 >> 16 & 0xFF));
            packet[15] = ((byte)(n2 >> 24 & 0xFF));
                     
            SetPacketSequence(packet);
            SetPacketCRC(packet);

            _client.Send(packet);
        }

        private void SetPacketSequence(byte[] packet)
        {
            packet[7] = (byte)(_controlSequence & 0xff);
            packet[8] = (byte)((_controlSequence >> 8) & 0xff);
            _controlSequence++;
        }

        private void SetPacketCRC(byte[] packet)
        {
            Crc.CalcUCRC(packet, 4);
            Crc.CalcCrc(packet, packet.Length);
        }

        private void Disconnect()
        {
            _cancelTokens.Cancel();
            _connected = false;

            if (_connectionState != ConnectionState.Disconnected)
            {
                OnConnection?.Invoke(this, ConnectionState.Disconnected);
            }

            _connectionState = ConnectionState.Disconnected;
        }

        private void ConnectClient()
        {
            _client = UdpUser.ConnectTo("192.168.10.1", 8889);

            _connectionState = ConnectionState.Connecting;
            OnConnection?.Invoke(this, _connectionState);

            byte[] connectPacket = Encoding.UTF8.GetBytes("conn_req:\x00\x00");
            connectPacket[connectPacket.Length - 2] = 0x96;
            connectPacket[connectPacket.Length - 1] = 0x17;
            _client.Send(connectPacket);
        }

        public void ConnectionSetPause(bool bPause)
        {
            if (bPause && _connectionState == ConnectionState.Connected)
            {
                _connectionState = ConnectionState.Paused;
                OnConnection?.Invoke(this, _connectionState);
            }
            else if (bPause == false && _connectionState == ConnectionState.Paused)
            {         
                OnConnection?.Invoke(this, ConnectionState.UnPausing);
                _connectionState = ConnectionState.Connected;
                OnConnection?.Invoke(this, ConnectionState.Connected);
            }
        }

        private byte[] picbuffer = new byte[3000 * 1024];
        private bool[] picChunkState;
        private bool[] picPieceState;
        private uint picBytesRecived;
        private uint picBytesExpected;
        public bool picDownloading;
        private ushort maxPieceNum = 0;

        private void StartListeners()
        {
            _cancelTokens = new CancellationTokenSource();
            CancellationToken token = _cancelTokens.Token;

            Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    try
                    {
                        if (token.IsCancellationRequested)//handle canceling thread.
                        {
                            break;
                        }

                        Received received = await _client.Receive();
                        lastMessageTime = DateTime.Now;

                        if (_connectionState == ConnectionState.Connecting)
                        {
                            if (received.Message.StartsWith("conn_ack"))
                            {
                                _connected = true;
                                _connectionState = ConnectionState.Connected;
                                OnConnection?.Invoke(this, _connectionState);

                                StartHeartbeat();
                                
                                Console.WriteLine("Tello Connected!");
                                continue;
                            }
                        }

                        if (received.bytes.Length < 10) return;
                        var pkt = BufferToPacket(received.bytes);
                        int cmdId = received.bytes[5] | (received.bytes[6] << 8);

                        var b = received.bytes.Skip(9).ToArray();
                        var pl = pkt.payload;

                        switch (pkt.messageID)
                        {
                            case MessageTypes.msgFileSize:
                                picFilePath = picPath + DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss") + ".jpg";
                                Messages.FileInfo fi = PayloadToFileInfo(pkt.payload);
                                fileTemp.fID = fi.fId;
                                fileTemp.filetype = fi.fileType;
                                fileTemp.expectedSize = fi.FileSize;
                                fileTemp.accumSize = 0;
                                fileTemp.filePieces = new FilePiece[1024];
                                SendAckFileSize();
                                break;
                            case MessageTypes.msgFileData:
                                int start = 9;
                                ushort fileNum = BitConverter.ToUInt16(received.bytes, start);
                                start += 2;
                                uint pieceNum = BitConverter.ToUInt32(received.bytes, start);
                                start += 4;
                                uint seqNum = BitConverter.ToUInt32(received.bytes, start);
                                start += 4;
                                ushort size = BitConverter.ToUInt16(received.bytes, start);
                                start += 2;

                                maxPieceNum = (ushort)Math.Max(pieceNum, maxPieceNum);
                                if (!picChunkState[seqNum])
                                {
                                    Array.Copy(received.bytes, start, picbuffer, seqNum * 1024, size);
                                    picBytesRecived += size;
                                    picChunkState[seqNum] = true;

                                    for (int p = 0; p < picChunkState.Length / 8; p++)
                                    {
                                        bool done = true;
                                        for (int s = 0; s < 8; s++)
                                        {
                                            if (!picChunkState[(p * 8) + s])
                                            {
                                                done = false;
                                                break;
                                            }
                                        }
                                        if (done && !picPieceState[p])
                                        {
                                            picPieceState[p] = true;
                                            SendAckFilePiece(0, fileNum, (uint)p);
                                        }
                                    }
                                    if (picFilePath != null && picBytesRecived >= picBytesExpected)
                                    {
                                        picDownloading = false;

                                        SendAckFilePiece(1, 0, maxPieceNum);
                                        SendAckFileDone((int)picBytesExpected);

                                        OnUpdate?.Invoke(this, new TelloStateEventArgs(State, 100));

                                        Console.WriteLine("\nDONE PN:" + pieceNum + " max: " + maxPieceNum);
                                                                                
                                        using FileStream? stream = new(picFilePath, FileMode.Append);
                                        stream.Write(picbuffer, 0, (int)picBytesExpected);
                                    }
                                }
                                break;
                            case MessageTypes.msgFlightStatus:
                                _state.Set(received.bytes.Skip(9).ToArray());
                                break;
                            case MessageTypes.msgLightStrength:
                                _state.LightStrength = pkt.payload[0];
                                _state.LightStrengthUpdated = DateTime.Now;
                                break;
                            case MessageTypes.msgLogConfig:
                                break;
                            case MessageTypes.msgLogHeader:                                
                                SendAckLogHeader(MessageTypes.msgLogHeader, BitConverter.ToUInt16(received.bytes, 9));
                                break;
                            case MessageTypes.msgLogData:
                                _state.ParseLog(received.bytes.Skip(10).ToArray());                                
                                break;
                            case MessageTypes.msgQueryHeightLimit:
                                _state.MaxHeight = pkt.payload[1];
                                break;
                            case MessageTypes.msgQueryLowBattThresh:
                                _state.LowBatteryThreshold = pkt.payload[1];
                                break;
                            case MessageTypes.msgQuerySSID:
                                _state.SSID = Encoding.ASCII.GetString(pkt.payload.Skip(2).ToArray());
                                break;
                            case MessageTypes.msgQueryVersion:
                                _state.Version = Encoding.ASCII.GetString(pkt.payload.Skip(1).ToArray());
                                break;
                            case MessageTypes.msgQueryVideoBitrate:
                                _state.VideoBitrate = (VBR)pkt.payload[0];
                                break;
                            case MessageTypes.msgSetDateTime:
                                SendDateTime();
                                break;
                            case MessageTypes.msgSetLowBattThresh:
                                break;
                            case MessageTypes.msgSmartVideoStatus:
                                break;
                            case MessageTypes.msgSwitchPicVideo:
                                break;
                            case MessageTypes.msgWifiStrength:
                                _state.WifiStrength = pkt.payload[0];
                                _state.WifiInterference = pkt.payload[1];
                                break;
                            default:
                                break;

                        }
                        OnUpdate?.Invoke(this, new TelloStateEventArgs(State, pkt.messageID));
                    }
                    catch (Exception eex)
                    {
                        Console.WriteLine("Receive thread error:" + eex.Message);
                        Disconnect();
                        break;
                    }
                }
            }, token);
        }
        
        private void SendFileDone(short fID, object accumSize)
        {
            var pkt = NewPacket(PacketType.ptGet, MessageTypes.msgFileDone, _controlSequence++, 6);
            pkt.payload[0] = (byte)fID;
            pkt.payload[1] = (byte)((byte)fID >> 8);
            pkt.payload[2] = (byte)accumSize;
            pkt.payload[3] = (byte)((byte)accumSize >> 8);
            pkt.payload[4] = (byte)((byte)accumSize >> 16);
            pkt.payload[5] = (byte)((byte)accumSize >> 24);
            _client.Send(PacketToBuffer(pkt));
        }

        private void SendFileAckPiece(byte done, short fID, uint pieceNum)
        {
            var pkt = NewPacket(PacketType.ptData1, MessageTypes.msgFileData, _controlSequence++, 7);
            pkt.payload[0] = done;
            pkt.payload[1] = (byte)fID;
            pkt.payload[2] = (byte)(fID >> 8);
            pkt.payload[3] = (byte)pieceNum;
            pkt.payload[4] = (byte)(pieceNum >> 8);
            pkt.payload[5] = (byte)((byte)pieceNum >> 16);
            pkt.payload[6] = (byte)((byte)pieceNum >> 24);
            _client.Send(PacketToBuffer(pkt));
        }

        public delegate float[] GetControllerDeligate();
        public GetControllerDeligate? GetControllerCallback;

        private void StartHeartbeat()
        {
            CancellationToken token = _cancelTokens.Token;

            Func<Task> function = Heartbeat(token);
            Task.Factory.StartNew(function, token);
        }

        private Func<Task> Heartbeat(CancellationToken token)
        {
            return async () =>
            {
                while (true)
                {

                    try
                    {
                        if (token.IsCancellationRequested)
                        {
                            break;
                        }

                        if (_connectionState == ConnectionState.Connected)
                        {
                            SendControllerUpdate();
                            UpdateSticks(new StickMessage() { Rx = 0, Ry = 0, Lx = 0, Ly = 0 });
                        }
                        Thread.Sleep(40);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Heatbeat error:" + ex.Message);
                        if (ex.Message.StartsWith("Access denied") && _connectionState != ConnectionState.Paused)
                        {
                            Console.WriteLine("Heatbeat: access denied and not paused:" + ex.Message);
                            Disconnect();
                            break;
                        }

                        //Denied means app paused
                        if (!ex.Message.StartsWith("Access denied"))
                        {
                            Disconnect();
                            break;
                        }
                    }
                }
            };
        }

        private void SendStickUpdate()
        {
            var pkt = new Packet
            {
                header = msgHdr,
                toDrone = true,
                packetType = PacketType.ptData2,
                messageID = MessageTypes.msgSetStick,
                sequence = 0,
                payload = new byte[11]
            };

            // This packing of the joystick data is just vile...
            int packedAxes = Int16ToTello(ctrlRx) & 0x07ff;
            packedAxes |= (Int16ToTello(ctrlRy) & 0x07ff) << 11;
            packedAxes |= (Int16ToTello(ctrlLy) & 0x07ff) << 22;
            packedAxes |= (Int16ToTello(ctrlLx) & 0x07ff) << 33;

            if (ctrlSportsMode)
            {
                packedAxes |= 1 << 44;
            }

            pkt.payload[0] = (byte)packedAxes;
            pkt.payload[1] = (byte)(packedAxes >> 8);
            pkt.payload[2] = (byte)(packedAxes >> 16);
            pkt.payload[3] = (byte)(packedAxes >> 24);
            pkt.payload[4] = (byte)(packedAxes >> 32);
            pkt.payload[5] = (byte)(packedAxes >> 40);

            var now = DateTime.Now;
            pkt.payload[6] = (byte)now.Hour;
            pkt.payload[7] = (byte)now.Minute;
            pkt.payload[8] = (byte)now.Second;
            pkt.payload[9] = (byte)(now.Millisecond & 0xff);
            pkt.payload[10] = (byte)(now.Millisecond >> 8);

            var buffer = PacketToBuffer(pkt);
            _client.Send(buffer);
        }

        public void Connect()
        {
            _ = Task.Factory.StartNew(async () =>
              {
                  TimeSpan timeout = new TimeSpan(3000);//3 second connection timeout
                  while (true)
                  {
                      try
                      {
                          switch (_connectionState)
                          {
                              case ConnectionState.Disconnected:
                                  ConnectClient();
                                  lastMessageTime = DateTime.Now;
                                  StartListeners();
                                  break;
                              case ConnectionState.Connecting:
                              case ConnectionState.Connected:
                                  TimeSpan elapsed = DateTime.Now - lastMessageTime;
                                  if (elapsed.Seconds > 30)
                                  {
                                      Console.WriteLine("Connection timeout :");
                                      Disconnect();
                                  }
                                  break;
                              case ConnectionState.Paused:
                                  lastMessageTime = DateTime.Now;
                                  break;
                          }
                          Thread.Sleep(100);
                      }
                      catch (Exception ex)
                      {
                          Console.WriteLine("Connection thread error:" + ex.Message);
                      }
                  }
              });
        }

        public ControllerState _autoPilotControllerState = new();

        private bool ctrlBouncing;
        private bool ctrlSportsMode;
        private short ctrlRx;
        private short ctrlRy;
        private short ctrlLy;
        private short ctrlLx;

        public float Clamp(float value, float min, float max)
        {
            return (value < min) ? min : (value > max) ? max : value;
        }

        public void SendControllerUpdate()
        {
            if (!_connected)
            {
                return;
            }

            float boost = 0.0f;
            if (ctrlSportsMode)
            {
                boost = 1.0f;
            }

            float rx = ctrlRx;
            float ry = ctrlRy;
            float lx = ctrlLx;
            float ly = ctrlLy;

            if (true)
            {
                rx = Clamp(rx + _autoPilotControllerState.Rx, -1.0f, 1.0f);
                ry = Clamp(ry + _autoPilotControllerState.Ry, -1.0f, 1.0f);
                lx = Clamp(lx + _autoPilotControllerState.Lx, -1.0f, 1.0f);
                ly = Clamp(ly + _autoPilotControllerState.Ly, -1.0f, 1.0f);
            }

            byte[]? packet = CreateJoyPacket(rx, ry, lx, ly, boost);

            try
            {
                _client.Send(packet);
            }
            catch (Exception)
            {
            }
        }

        //Create joystick packet from floating point axis.
        //Center = 0.0. 
        //Up/Right =1.0. 
        //Down/Left=-1.0. 
        private byte[] CreateJoyPacket(float fRx, float fRy, float fLx, float fLy, float speed)
        {
            //template joy packet.
            byte[]? packet = new byte[] { 0xcc, 0xb0, 0x00, 0x7f, 0x60, 0x50, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x12, 0x16, 0x01, 0x0e, 0x00, 0x25, 0x54 };

            short axis1 = (short)(660.0F * fRx + 1024.0F);//RightX center=1024 left =364 right =-364
            short axis2 = (short)(660.0F * fRy + 1024.0F);//RightY down =364 up =-364
            short axis3 = (short)(660.0F * fLy + 1024.0F);//LeftY down =364 up =-364
            short axis4 = (short)(660.0F * fLx + 1024.0F);//LeftX left =364 right =-364
            short axis5 = (short)(660.0F * speed + 1024.0F);//Speed. 

            if (speed > 0.1f)
            {
                axis5 = 0x7fff;
            }

            long packedAxis = ((long)axis1 & 0x7FF) | (((long)axis2 & 0x7FF) << 11) | ((0x7FF & (long)axis3) << 22) | ((0x7FF & (long)axis4) << 33) | ((long)axis5 << 44);
            packet[9] = ((byte)(int)(0xFF & packedAxis));
            packet[10] = ((byte)(int)(packedAxis >> 8 & 0xFF));
            packet[11] = ((byte)(int)(packedAxis >> 16 & 0xFF));
            packet[12] = ((byte)(int)(packedAxis >> 24 & 0xFF));
            packet[13] = ((byte)(int)(packedAxis >> 32 & 0xFF));
            packet[14] = ((byte)(int)(packedAxis >> 40 & 0xFF));

            //Add time info.		
            DateTime now = DateTime.Now;
            packet[15] = (byte)now.Hour;
            packet[16] = (byte)now.Minute;
            packet[17] = (byte)now.Second;
            packet[18] = (byte)(now.Millisecond & 0xff);
            packet[19] = (byte)(now.Millisecond >> 8);

            Crc.CalcUCRC(packet, 4);//Not really needed.            
            Crc.CalcCrc(packet, packet.Length);

            return packet;
        }
    }
}