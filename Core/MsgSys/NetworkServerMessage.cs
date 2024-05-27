namespace KazNet.Core
{
    public class NetworkServerMessage : NetworkClientMessage
    {
        public ClientEntity client;

        public NetworkServerMessage(ClientEntity _client, ushort _handlerId, byte[] _data) : base(_handlerId, _data)
        {
            client = _client;
        }
    }
}
