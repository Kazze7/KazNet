using System.Net.Sockets;

namespace KazNet.MsgSys
{
    public class NetworkClient
    {
        public TcpClient tcpClient;
        public NetworkPermissionGroup permissionGroup;

        public NetworkClient(TcpClient _tcpClient, NetworkPermissionGroup _permissionGroup)
        {
            tcpClient = _tcpClient;
            permissionGroup = _permissionGroup;
        }
    }
}
