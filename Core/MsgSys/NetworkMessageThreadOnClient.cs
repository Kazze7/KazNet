namespace KazNet.Core
{
    class NetworkMessageThreadOnClient
    {
        public NetworkMessageHandler handler;
        public NetworkClientMessage message;

        public NetworkMessageThreadOnClient(NetworkMessageHandler _handler, NetworkClientMessage _message)
        {
            handler = _handler;
            message = _message;
        }
    }
}
