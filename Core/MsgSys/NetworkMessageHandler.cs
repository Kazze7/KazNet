namespace KazNet.Core
{
    public abstract class NetworkMessageHandler
    {
        public NetworkMessagePermission permission;
        public NetworkMessageThread thread;

        public abstract void OnClient(NetworkClient _client, NetworkClientMessage _message);
        public abstract void OnServer(NetworkServer _server, NetworkServerMessage _message);

        public NetworkMessageHandler(NetworkMessagePermission _permission, NetworkMessageThread? _thread)
        {
            permission = _permission;
            thread = _thread;
        }
    }
}
