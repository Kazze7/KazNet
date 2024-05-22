using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace KazNet.MsgSys
{
    public class NetworkClient
    {
        public TcpClient tcpClient;
        public string id;
        public NetworkPermissionGroup permissionGroup;
    }
}
