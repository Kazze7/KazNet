using System.Net.Sockets;

namespace KazNet.Core
{
    public class Packet
    {
        public Socket socket;
        public byte[] data;

        public Packet(Socket _socket, byte[] _data)
        {
            socket = _socket;
            data = _data;
        }
        public Packet(Socket _socket, List<byte> _data)
        {
            socket = _socket;
            data = _data.ToArray();
        }
    }
}
