using KazDev.Core;
using System.Net.Sockets;
using System.Net;

namespace KazNet.Core
{
    public class TCPClient
    {
        bool isRunning = false;
        public NetworkStatus GetNetworkStatus { get => isRunning ? NetworkStatus.launched : NetworkStatus.stopped; }

        NetworkConfig networkConfig;
        public string Address { get => networkConfig.address; }
        public ushort Port { get => networkConfig.port; }

        Client client;
        AutoResetEvent clientEvent = new(true);

        public delegate void NetworkStatusMethod(NetworkStatus _networkStatus, Socket? _socket);
        NetworkStatusMethod networkStatusMethod;
        public delegate void ConnectMethod(Socket _socket);
        ConnectMethod connectMethod;
        public delegate void DisconnectMethod(Socket _socket);
        DisconnectMethod disconnectMethod;
        public delegate void DecodeMethod(Packet _packet);
        DecodeMethod decodeMethod;

        public TCPClient(
            NetworkConfig _networkConfig,
            NetworkStatusMethod _networkStatus = null,
            ConnectMethod _connect = null,
            DisconnectMethod _disconnect = null,
            DecodeMethod _decodePacket = null
            )
        {
            networkConfig = _networkConfig;
            networkStatusMethod = _networkStatus;
            connectMethod = _connect;
            disconnectMethod = _disconnect;
            decodeMethod = _decodePacket;
        }

        public void Start()
        {
            clientEvent.WaitOne();
            if (!isRunning)
            {
                isRunning = true;
                SendNetworkStatus(NetworkStatus.started);
                StartConnection();
            }
            clientEvent.Set();
        }
        public void Stop()
        {
            clientEvent.WaitOne();
            if (isRunning)
            {
                isRunning = false;
                //  Close threads
                client.network.sendingWorker.Stop();
                client.network.receivingWorker.Stop();
                client.network.connectionWorker.Join();
                //  Close client socket
                client.socket.Close();
                SendNetworkStatus(NetworkStatus.stopped);
            }
            clientEvent.Set();
        }

        void SendNetworkStatus(NetworkStatus _networkStatus) { SendNetworkStatus(_networkStatus, null); }
        void SendNetworkStatus(NetworkStatus _networkStatus, Socket? _socket)
        {
            networkStatusMethod?.Invoke(_networkStatus, _socket);
        }

        void StartConnection()
        {
            IPAddress ipAddress;
            try
            {
                if (!IPAddress.TryParse(networkConfig.address, out ipAddress))
                {
                    IPHostEntry ipHostInfo = Dns.GetHostEntry(networkConfig.address);
                    ipAddress = ipHostInfo.AddressList.Where(x => x.AddressFamily == AddressFamily.InterNetwork).FirstOrDefault();
                }
                client = new Client(new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp), new NetworkThread(), networkConfig.bufferSize);
            }
            catch (Exception exception)
            {
                SendNetworkStatus(NetworkStatus.errorConnection);
                Stop();
                //  Log to file
                //  Console.WriteLine(exception.ToString());
                return;
            }
            client.network.connectionWorker = new Thread(() =>
            {
                client.socket.BeginConnect(new IPEndPoint(ipAddress, networkConfig.port), new AsyncCallback(AcceptConnection), client);
            });
            client.network.receivingWorker = new QueueWorker<Packet>(_packet => { decodeMethod?.Invoke(_packet); });
            client.network.sendingWorker = new QueueWorker<Packet>(_packet => { SendPacket(_packet); });
            client.network.sendingWorker.Start();
            client.network.receivingWorker.Start();
            client.network.connectionWorker.Start();
        }
        void AcceptConnection(IAsyncResult _asyncResult)
        {
            try
            {
                Client client = (Client) _asyncResult.AsyncState;
                client.socket.EndConnect(_asyncResult);
                networkConfig.SetConfig(client.socket);
                SendNetworkStatus(NetworkStatus.connected);
                connectMethod?.Invoke(client.socket);
                client.socket.BeginReceive(client.buffer, 0, networkConfig.bufferSize, SocketFlags.None, new AsyncCallback(ReceivePacket), client);
            }
            catch (Exception exception)
            {
                SendNetworkStatus(NetworkStatus.errorConnection, client.socket);
                Stop();
                //  Log to file
                //  Console.WriteLine(exception.ToString());
            }
        }
        void ReceivePacket(IAsyncResult _asyncResult)
        {
            Client client = (Client)_asyncResult.AsyncState;
            try
            {
                int packetSize = client.socket.EndReceive(_asyncResult);
                if (packetSize > 0)
                {
                    int index = 0;
                    int packetLength;
                    byte[] packetData;
                    do
                    {
                        packetLength = BitConverter.ToUInt16(client.buffer, index);
                        index += 2;
                        packetData = new byte[packetLength];
                        Array.Copy(client.buffer, index, packetData, 0, packetLength);
                        client.network.receivingWorker.Enqueue(new Packet(client.socket, packetData));
                        index += packetLength;
                    }
                    while (index < packetSize);
                    client.socket.BeginReceive(client.buffer, 0, networkConfig.bufferSize, SocketFlags.None, new AsyncCallback(ReceivePacket), client);
                    return;
                }
            }
            catch (Exception exception)
            {
                //  Log to file
                //  Console.WriteLine(exception.ToString());
            }
            SendNetworkStatus(NetworkStatus.errorRecivePacket, client.socket);
            Disconnect();
        }
        void SendPacket(Packet _packet)
        {
            try
            {
                byte[] packetData = new byte[_packet.data.Length + 2];
                Array.Copy(BitConverter.GetBytes((ushort)_packet.data.Length), packetData, 2);
                Array.Copy(_packet.data, 0, packetData, 2, _packet.data.Length);
                _packet.socket.BeginSend(packetData, 0, packetData.Length, SocketFlags.None, new AsyncCallback(SendPacketComplete), _packet.socket);
                client.network.nextSendEvent.WaitOne();
            }
            catch (Exception exception)
            {
                SendNetworkStatus(NetworkStatus.errorSendPacket, _packet.socket);
                //  Log to file
                //  Console.WriteLine(exception.ToString());
            }
        }
        void SendPacketComplete(IAsyncResult _asyncResult)
        {
            client.network.nextSendEvent.Set();
            Socket socket = (Socket)_asyncResult.AsyncState;
            try
            {
                socket.EndSend(_asyncResult);
            }
            catch (Exception exception)
            {
                SendNetworkStatus(NetworkStatus.errorSendPacket, socket);
                //  Log to file
                //  Console.WriteLine(exception.ToString());
            }
        }

        public void Send(byte[] _data) { Send(new Packet(client.socket, _data)); }
        public void Send(List<byte> _data) { Send(new Packet(client.socket, _data.ToArray())); }
        public void Send(Packet _packet) { client.network.sendingWorker.Enqueue(_packet); }
        public void Disconnect()
        {
            disconnectMethod?.Invoke(client.socket);
            Stop();
        }
    }
}
