using System.Net;
using System.Net.Sockets;

namespace KazNet.Core
{
    public class NetworkConfig
    {
        public string address = "127.0.0.1";
        public IPAddress IPAddress
        {
            get
            {
                IPAddress ipAddress = IPAddress.Any;
                if (!IPAddress.TryParse(address, out ipAddress))
                {
                    IPHostEntry ipHostInfo = Dns.GetHostEntry(address);
                    ipAddress = ipHostInfo.AddressList.Where(x => x.AddressFamily == AddressFamily.InterNetwork).FirstOrDefault();
                }
                return ipAddress;
            }
        }
        public IPEndPoint IPEndPoint { get => new IPEndPoint(IPAddress, port); }
        public ushort port = 12345;
        public ushort bufferSize = 8192;
        public int timeout = 1000;
        public bool noDelay = true;
        public bool useSsl = false;
        public string sslTargetHost = "";

        public void SetConfig(Socket _socket)
        {
            _socket.NoDelay = noDelay;
            _socket.ReceiveBufferSize = bufferSize;
            _socket.ReceiveTimeout = timeout;
            _socket.SendBufferSize = bufferSize;
            _socket.SendTimeout = timeout;
        }
        public void SetConfig(TcpClient _tcpClient) { SetConfig(_tcpClient.Client); }
    }
    public class ServerNetworkConfig : NetworkConfig
    {
        public int backLog = 100;
        public ushort maxConnections = 100;
        public string sslFilePathPfx = "";
        public string sslFilePassword = "";

        public void SetConfig(TcpListener _tcpListener) { SetConfig(_tcpListener.Server); }
    }
}
