using System.Net.Sockets;

namespace KazNet.Core
{
    class Client
    {
        public TcpClient tcpClient;
        public Stream stream;
        public NetworkThread networkThread;
        public byte[] buffer;
        public List<byte> data = new List<byte>();

        public Client(TcpClient _tcpClient, NetworkThread _networkThread, ushort _bufferSize)
        {
            tcpClient = _tcpClient;
            networkThread = _networkThread;
            buffer = new byte[_bufferSize];
        }
    }
}
