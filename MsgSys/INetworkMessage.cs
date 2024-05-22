namespace KazNet.MsgSys
{
    public interface INetworkMessage
    {
        byte[] Serialize();
        void Deserialize(ref byte[] _data);
    }
}
