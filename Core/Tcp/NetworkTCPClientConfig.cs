using System.Net;
using System.Net.Sockets;

namespace KazNet.Tcp
{
    public class NetworkTCPClientConfig
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

        public void SetConfig(TcpClient _tcpClient)
        {
            _tcpClient.NoDelay = noDelay;
            _tcpClient.ReceiveBufferSize = bufferSize;
            _tcpClient.ReceiveTimeout = timeout;
            _tcpClient.SendBufferSize = bufferSize;
            _tcpClient.SendTimeout = timeout;
        }
    }
}
