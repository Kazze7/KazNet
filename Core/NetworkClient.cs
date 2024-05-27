namespace KazNet.Core
{
    public abstract class NetworkClient
    {
        public virtual void SendMessage(NetworkMessageHandler _messageHandler, INetworkMessage _networkMessage) { }
    }
}
