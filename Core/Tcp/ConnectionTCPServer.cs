using KazNet.Core;
using System.Net.Sockets;

namespace KazNet.Tcp
{
    class ConnectionTCPServer
    {
        public TcpClient tcpClient;
        public Stream stream;
        public ClientEntity networkClient;
        public ConnectionServerThreads connectionThreads;
        public byte[] buffer;
        public List<byte> data = new List<byte>();

        public ConnectionTCPServer(TcpClient _tcpClient, Stream _stream, ClientEntity _networkClient, ConnectionServerThreads _connectionThreads, ushort _bufferSize)
        {
            tcpClient = _tcpClient;
            stream = _stream;
            networkClient = _networkClient;
            connectionThreads = _connectionThreads;
            buffer = new byte[_bufferSize];
        }

        public void Close()
        {
            stream.Close();
            tcpClient.Close();
        }
    }
}
