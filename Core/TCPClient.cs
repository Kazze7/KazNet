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
            client.network.receivingWorker = new QueueWorker<Packet>(Decode);
            client.network.sendingWorker = new QueueWorker<Packet>(SendPacket);
            client.network.sendingWorker.Start();
            client.network.receivingWorker.Start();
            client.network.connectionWorker.Start();
        }
        void AcceptConnection(IAsyncResult _asyncResult)
        {
            try
            {
                Client client = (Client)_asyncResult.AsyncState;
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
                    int dataLength;
                    int packetCounter;
                    byte[] packetData;
                    do
                    {
                        dataLength = BitConverter.ToUInt16(client.buffer, index) - 4;
                        index += 2;
                        packetCounter = BitConverter.ToUInt16(client.buffer, index);
                        index += 2;
                        packetData = new byte[dataLength];
                        Array.Copy(client.buffer, index, packetData, 0, dataLength);
                        client.data.AddRange(packetData);
                        if (packetCounter == 0)
                        {
                            client.network.receivingWorker.Enqueue(new Packet(client.socket, client.data));
                            client.data = new();
                        }
                        index += dataLength;
                    }
                    while (index < packetSize);
                    client.socket.BeginReceive(client.buffer, 0, networkConfig.bufferSize, SocketFlags.None, new AsyncCallback(ReceivePacket), client);
                    return;
                }
            }
            catch (Exception exception)
            {
                SendNetworkStatus(NetworkStatus.errorRecivePacket, client.socket);
                Disconnect();
                //  Log to file
                //  Console.WriteLine(exception.ToString());
            }
        }
        void SendPacket(Packet _packet)
        {
            try
            {
                int index = 0;
                int dataLength = networkConfig.bufferSize - 4;
                int packetCounter = _packet.data.Length / dataLength - ((_packet.data.Length % dataLength == 0) ? 1 : 0); ;
                byte[] packetData;
                for (int i = packetCounter; i > 0; i--)
                {
                    packetData = new byte[networkConfig.bufferSize];
                    Array.Copy(BitConverter.GetBytes((ushort)networkConfig.bufferSize), packetData, 2);
                    Array.Copy(BitConverter.GetBytes((ushort)i), 0, packetData, 2, 2);
                    Array.Copy(_packet.data, index, packetData, 4, dataLength);
                    index += dataLength;
                    _packet.socket.BeginSend(packetData, 0, packetData.Length, SocketFlags.None, new AsyncCallback(SendPacketComplete), _packet.socket);
                    client.network.nextSendEvent.WaitOne();
                }
                dataLength = _packet.data.Length - index;
                packetData = new byte[dataLength + 4];
                Array.Copy(BitConverter.GetBytes((ushort)packetData.Length), packetData, 2);
                Array.Copy(BitConverter.GetBytes((ushort)0), 0, packetData, 2, 2);
                Array.Copy(_packet.data, index, packetData, 4, dataLength);
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
        void Decode(Packet _packet) { decodeMethod?.Invoke(_packet); }

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
