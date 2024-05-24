namespace KazNet.MsgSys
{
    class NetworkThreadData
    {
        public NetworkMessageHandler messageHandler;
        public NetworkMessage message;

        public NetworkThreadData(NetworkMessageHandler _messageHandler, NetworkMessage _message)
        {
            messageHandler = _messageHandler;
            message = _message;
        }
    }
}
