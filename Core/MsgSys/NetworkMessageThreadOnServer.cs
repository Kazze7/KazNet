namespace KazNet.Core
{
    class NetworkMessageThreadOnServer
    {
        public NetworkMessageHandler handler;
        public NetworkServerMessage message;

        public NetworkMessageThreadOnServer(NetworkMessageHandler _handler, NetworkServerMessage _message)
        {
            handler = _handler;
            message = _message;
        }
    }
}
