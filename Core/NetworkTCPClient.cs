namespace KazNet.Core
{
    public abstract class NetworkTCPClient
    {
        TCPClient tcpClient;

        public NetworkTCPClient(NetworkConfig _networkConfig)
        {
            tcpClient = new TCPClient(_networkConfig, NetworkStatus, Connection, Disconnection, Decode);
        }

        public virtual void Start() { tcpClient.Start(); }
        public virtual void Stop() { tcpClient.Stop(); }

        public bool IsRunning { get => tcpClient.IsRunning; }
        public string Address { get => tcpClient.Address; }
        public ushort Port { get => tcpClient.Port; }

        public void Send(byte[] _data) { tcpClient.Send(_data); }
        public void Send(List<byte> _data) { tcpClient.Send(_data); }
        public void Disconnect() { tcpClient.Disconnect(); }

        public abstract void NetworkStatus(NetworkStatus _networkStatus, string? _exception);
        public abstract void Connection();
        public abstract void Disconnection();
        public abstract void Decode(byte[] _data);
    }
}
