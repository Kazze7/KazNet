using System.Net.Sockets;

namespace KazNet.Core
{
    public abstract class NetworkTCPClient
    {
        TCPClient tcpClient;

        public NetworkTCPClient(NetworkConfig _networkConfig)
        {
            tcpClient = new TCPClient(_networkConfig, NetworkStatus, Connection, Disconnection, Decode);
        }

        public void Start() { tcpClient.Start(); }
        public void Stop() { tcpClient.Stop(); }

        public bool IsRunning { get => tcpClient.IsRunning; }
        public string Address { get => tcpClient.Address; }
        public ushort Port { get => tcpClient.Port; }

        public void Send(byte[] _data) { tcpClient.Send(_data); }
        public void Send(List<byte> _data) { tcpClient.Send(_data); }
        public void Send(NetworkPacket _networkPacket) { tcpClient.Send(_networkPacket); }
        public void Disconnect() { tcpClient.Disconnect(); }

        public abstract void NetworkStatus(NetworkStatus _networkStatus, TcpClient? _tcpClient, string? _exception);
        public abstract void Connection(TcpClient _tcpClient);
        public abstract void Disconnection(TcpClient _tcpClient);
        public abstract void Decode(NetworkPacket _networkPacket);
    }
}
