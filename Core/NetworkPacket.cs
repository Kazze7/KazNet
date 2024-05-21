using System.Net.Sockets;

namespace KazNet.Core
{
    public class NetworkPacket
    {
        public TcpClient tcpClient;
        public byte[] data;

        public NetworkPacket(TcpClient _tcpClient, byte[] _data)
        {
            tcpClient = _tcpClient;
            data = _data;
        }
        public NetworkPacket(TcpClient _tcpClient, List<byte> _data)
        {
            tcpClient = _tcpClient;
            data = _data.ToArray();
        }
    }
}
