using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TelloSharp
{
    public struct Received
    {
        public IPEndPoint Sender;
        public string Message;
        public byte[] bytes;
    }

    public abstract class UdpBase
    {
        public UdpClient Client;

        protected UdpBase()
        {
            Client = new UdpClient();
        }

        public async Task<Received> Receive()
        {
            UdpReceiveResult result = await Client.ReceiveAsync();
            return new Received()
            {
                bytes = result.Buffer.ToArray(),
                Message = Encoding.ASCII.GetString(result.Buffer, 0, result.Buffer.Length),
                Sender = result.RemoteEndPoint
            };
        }
    }

    //Server
    public class UdpListener : UdpBase
    {
        private readonly IPEndPoint _listenOn;

        public UdpListener(int port) : this(new IPEndPoint(IPAddress.Any, port))
        {
        }

        public UdpListener(IPEndPoint endpoint)
        {
            _listenOn = endpoint;
            Client = new UdpClient(_listenOn);
        }

        public void Reply(string message, IPEndPoint endpoint)
        {
            byte[]? datagram = Encoding.ASCII.GetBytes(message);
            Client.Send(datagram, datagram.Length, endpoint);
        }

    }

    //Client
    public class UdpUser : UdpBase
    {
        public UdpUser() { }

        public static UdpUser ConnectTo(string hostname, int port)
        {
            UdpUser? connection = new();
            connection.Client.Connect(hostname, port);
            return connection;
        }

        public void Send(string message)
        {
            byte[]? datagram = Encoding.ASCII.GetBytes(message);
            Client.Send(datagram, datagram.Length);
        }
        public void Send(byte[] message)
        {
            Client.Send(message, message.Length);
        }
    }
}