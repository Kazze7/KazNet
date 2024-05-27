namespace KazNet.Core
{
    public class NetworkClientMessage
    {
        public ushort handlerId;
        public byte[] data;

        public NetworkClientMessage(ushort _handlerId, byte[] _data)
        {
            handlerId = _handlerId;
            data = _data;
        }
    }
}
