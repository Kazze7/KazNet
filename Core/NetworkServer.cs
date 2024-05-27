namespace KazNet.Core
{
    public abstract class NetworkServer
    {
        public virtual void SendMessage(ClientEntity _clientEntity, NetworkMessageHandler _messageHandler, INetworkMessage _networkMessage) { }
    }
}
