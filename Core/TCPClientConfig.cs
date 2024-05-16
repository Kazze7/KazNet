using System.Net.Sockets;

namespace KazNet.Core
{
    public class TCPClientConfig
    {
        public string address = "127.0.0.1";
        public ushort port = 12345;
        public ushort bufferSize = 8192;
        public int timeout = 1000;
        public bool noDelay = true;

        public void SetClientConfig(Socket _socket)
        {
            _socket.NoDelay = noDelay;
            _socket.ReceiveBufferSize = bufferSize;
            _socket.ReceiveTimeout = timeout;
            _socket.SendBufferSize = bufferSize;
            _socket.SendTimeout = timeout;
        }
    }
}
