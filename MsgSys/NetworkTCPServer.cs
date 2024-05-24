using KazDev.Core;
using KazNet.Core;
using System.Collections.Concurrent;
using System.Net.Sockets;

namespace KazNet.MsgSys
{
    public abstract class NetworkTCPServer : Core.NetworkTCPServer
    {
        ConcurrentDictionary<TcpClient, NetworkClient> clientsByTcpClient = new();
        ConcurrentDictionary<NetworkThread, QueueWorker<NetworkThreadData>> networkThreads = new();
        public NetworkMessageHandlerList messageHandlerList = new();
        public NetworkPermissionGroup newConnectionPermissionGroup = new();

        public NetworkTCPServer(ServerNetworkConfig _networkConfig) : base(_networkConfig) { }

        public override void Stop()
        {
            base.Stop();
            networkThreads.ToList().ForEach(networkThread =>
            {
                networkThread.Value.Stop();
                networkThreads.TryRemove(networkThread);
            });
            networkThreads = new();
        }

        public abstract void Connection(NetworkClient _networkClient);
        public override void Connection(TcpClient _tcpClient)
        {
            NetworkClient networkClient = new NetworkClient(_tcpClient, newConnectionPermissionGroup);
            if (clientsByTcpClient.TryAdd(networkClient.tcpClient, networkClient))
                Connection(networkClient);
        }
        public abstract void Disconnection(NetworkClient _networkClient);
        public override void Disconnection(TcpClient _tcpClient)
        {
            if (clientsByTcpClient.TryRemove(_tcpClient, out NetworkClient networkClient))
                Disconnection(networkClient);
        }
        public override void Decode(NetworkPacket _networkPacket)
        {
            if (clientsByTcpClient.TryGetValue(_networkPacket.tcpClient, out NetworkClient networkClient))
            {
                short messageId = BitConverter.ToInt16(_networkPacket.data.Take(2).ToArray());
                if (messageHandlerList.GetMessageHandler(messageId, out NetworkMessageHandler messageHandler))
                    if (messageHandler != null)
                    {
                        //  Check permission
                        if (messageHandler.permission == null || networkClient.permissionGroup == null)
                            return;
                        if (!networkClient.permissionGroup.CheckPermission(messageHandler.permission))
                            return;
                        _networkPacket.data = _networkPacket.data.Skip(2).ToArray();
                        NetworkMessage message = new NetworkMessage(networkClient, messageId, _networkPacket.data);
                        //  QueueThread
                        if (messageHandler.thread != null)
                        {
                            if (!networkThreads.TryGetValue(messageHandler.thread, out QueueWorker<NetworkThreadData> queueWorker))
                                queueWorker.Enqueue(new NetworkThreadData(messageHandler, message));
                            else
                            {
                                queueWorker = new QueueWorker<NetworkThreadData>(Decode);
                                networkThreads.TryAdd(messageHandler.thread, queueWorker);
                                queueWorker.Start();
                            }
                            queueWorker.Enqueue(new NetworkThreadData(messageHandler, message));
                        }
                        //  Decode
                        messageHandler.OnServer(this, message);
                    }
            }
        }
        void Decode(NetworkThreadData _networkThreadData) { _networkThreadData.messageHandler.OnServer(this, _networkThreadData.message); }

        //public void Send(NetworkMessage _networkMessage) { Send(new NetworkPacket(_networkMessage.networkClient.tcpClient, BitConverter.GetBytes(_networkMessage.messageId).Concat(_networkMessage.data).ToArray())); }
    }
}
