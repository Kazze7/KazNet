using System.Net.Sockets;

namespace KazNet.Core
{
    class Client
    {
        public TcpClient tcpClient;
        Stream stream;
        public Stream Stream
        {
            get
            {
                if (stream != null)
                {
                    return stream;
                }
                else
                {
                    streamEvent.WaitOne();
                    return stream;
                }
            }
            set
            {
                stream = value;
            }
        }
        public AutoResetEvent streamEvent = new AutoResetEvent(false);
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
