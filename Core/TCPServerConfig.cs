using System.Net.Sockets;

namespace KazNet.Core
{
    public class TCPServerConfig : TCPClientConfig
    {
        public int backLog = 100;
        public ushort maxClients = 100;

        public void SetServerConfig(Socket _socket)
        {
            //_socket.Bind(new IPEndPoint(IPAddress.Any, port));
            SetClientConfig(_socket);
            //_socket.Listen(backLog);
        }
    }
}
