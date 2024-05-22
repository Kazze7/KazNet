using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KazNet.MsgSys
{
    public abstract class NetworkMessageHandler
    {
        public NetworkPermission permission;
        public NetworkThread thread;
        //public virtual void OnClient(NetTCPClient _netTCPClient, NetPacket _netPacket) { }
        //public virtual void OnServer(NetTCPServer _netTCPServer, NetPacket _netPacket) { }

        public NetworkMessageHandler(NetworkPermission _permission = null, NetworkThread _thread = null)
        {
            permission = _permission;
            thread = _thread;
            NetworkMessageHandlerList.Add(this);
        }
    }
}
