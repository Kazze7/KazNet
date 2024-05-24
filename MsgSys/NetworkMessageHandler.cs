namespace KazNet.MsgSys
{
    public abstract class NetworkMessageHandler
    {
        public NetworkPermission permission;
        public NetworkThread thread;

        public abstract void OnClient(NetworkTCPClient _networkTCPClient, NetworkMessage _networkMessage);
        public abstract void OnServer(NetworkTCPServer _networkTCPServer, NetworkMessage _networkMessage);

        public NetworkMessageHandler(NetworkPermission _permission, NetworkThread? _thread)
        {
            permission = _permission;
            thread = _thread;
        }
    }
}
