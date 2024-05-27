namespace KazNet.Tcp
{
    public class NetworkTCPServerConfig : NetworkTCPClientConfig
    {
        public int backLog = 100;
        public ushort maxConnections = 100;
        public string sslFilePathPfx = "";
        public string sslFilePassword = "";
    }
}
