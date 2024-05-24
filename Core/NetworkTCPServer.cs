using System.Net.Sockets;

namespace KazNet.Core
{
    public abstract class NetworkTCPServer
    {
        TCPServer tcpServer;

        public NetworkTCPServer(ServerNetworkConfig _networkConfig)
        {
            tcpServer = new TCPServer(_networkConfig, NetworkStatus, Connection, Disconnection, Decode);
        }

        public virtual void Start() { tcpServer.Start(); }
        public virtual void Stop() { tcpServer.Stop(); }

        public bool IsRunning { get => tcpServer.IsRunning; }
        public string Address { get => tcpServer.Address; }
        public ushort Port { get => tcpServer.Port; }
        public int ConnectionCount { get => tcpServer.ConnectionCount; }

        public void Send(TcpClient _tcpClient, byte[] _data) { tcpServer.Send(new NetworkPacket(_tcpClient, _data)); }
        public void Send(TcpClient _tcpClient, List<byte> _data) { tcpServer.Send(new NetworkPacket(_tcpClient, _data)); }
        public void Send(NetworkPacket _networkPacket) { tcpServer.Send(_networkPacket); }
        public void Disconnect(TcpClient _tcpClient) { tcpServer.Disconnect(_tcpClient); }
        public void DisconnectAll() { tcpServer.DisconnectAllClients(); }

        public abstract void NetworkStatus(NetworkStatus _networkStatus, TcpClient? _tcpClient, string? _exception);
        public abstract void Connection(TcpClient _tcpClient);
        public abstract void Disconnection(TcpClient _tcpClient);
        public abstract void Decode(NetworkPacket _networkPacket);
    }
}
