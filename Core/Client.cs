using System.Net.Sockets;

namespace KazNet.Core
{
    class Client
    {
        public Socket socket;
        public ServerThread serverThread;
        public byte[] buffer;

        public Client(Socket _socket, ServerThread _serverThread, ushort _bufferSize)
        {
            socket = _socket;
            serverThread = _serverThread;
            buffer = new byte[_bufferSize];
        }
    }
}
