namespace KazNet.MsgSys
{
    public class NetworkMessage
    {
        public NetworkClient networkClient;
        public short messageId;
        public byte[] data;

        public NetworkMessage(NetworkClient? _networkClient, short _messageId, byte[] _data)
        {
            networkClient = _networkClient;
            messageId = _messageId;
            data = _data;
        }
    }
}
