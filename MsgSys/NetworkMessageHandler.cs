namespace KazNet.MsgSys
{
    public abstract class NetworkMessageHandler
    {
        public NetworkPermission permission;
        public NetworkThread thread;

        public virtual void OnClient(NetworkTCPClient _networkTCPClient, NetworkMessage _networkMessage) { }
        public virtual void OnServer(NetworkTCPServer _networkTCPServer, NetworkMessage _networkMessage) { }

        public NetworkMessageHandler(NetworkPermission? _permission = null, NetworkThread? _thread = null)
        {
            permission = _permission;
            thread = _thread;
        }
    }
}
