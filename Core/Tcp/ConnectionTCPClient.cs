using KazNet.Core;
using System.Net.Sockets;

namespace KazNet.Tcp
{
    class ConnectionTCPClient
    {
        public TcpClient tcpClient;
        public Stream stream;
        public ConnectionClientThreads connectionThreads;
        public byte[] buffer;
        public List<byte> data = new List<byte>();

        public ConnectionTCPClient(TcpClient _tcpClient, ConnectionClientThreads _connectionThreads, ushort _bufferSize)
        {
            tcpClient = _tcpClient;
            connectionThreads = _connectionThreads;
            buffer = new byte[_bufferSize];
        }

        public void Close()
        {
            stream?.Close();
            tcpClient?.Close();
        }
    }
}
