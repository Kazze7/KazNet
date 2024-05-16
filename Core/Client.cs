using System.Net.Sockets;

namespace KazNet.Core
{
    class Client
    {
        public Socket socket;
        public NetworkThread network;
        public byte[] buffer;
        public List<byte> data = new List<byte>();

        public Client(Socket _socket, NetworkThread _networkThread, ushort _bufferSize)
        {
            socket = _socket;
            network = _networkThread;
            buffer = new byte[_bufferSize];
        }
    }
}
