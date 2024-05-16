using KazDev.Core;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace KazNet.Core
{
    public class TCPServer
    {
        ServerNetworkConfig networkConfig;
        public string Address { get => networkConfig.address; }
        public ushort Port { get => networkConfig.port; }

        NetworkStatus networkStatus = NetworkStatus.stopped;
        public NetworkStatus GetNetworkStatus { get => networkStatus; }

        Socket serverSocket;
        ConcurrentDictionary<int, NetworkThread> networkThreads = new ConcurrentDictionary<int, NetworkThread>();
        ConcurrentDictionary<Socket, Client> clients = new ConcurrentDictionary<Socket, Client>();

        public delegate void NetworkStatusMethod(NetworkStatus _networkStatus, Socket? _socket);
        NetworkStatusMethod networkStatusMethod;
        public delegate void ConnectMethod(Socket _socket);
        ConnectMethod connectMethod;
        public delegate void DisconnectMethod(Socket _socket);
        DisconnectMethod disconnectMethod;
        public delegate void DecodeMethod(Packet _packet);
        DecodeMethod decodeMethod;

        public TCPServer(
            ServerNetworkConfig _networkConfig,
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
            if (networkStatus == NetworkStatus.stopped)
            {
                ChangeNetworkStatus(NetworkStatus.started);
                StartListener();
            }
        }
        public void Stop()
        {
            if (networkStatus != NetworkStatus.started)
            {
                ChangeNetworkStatus(NetworkStatus.stopped);
                //  Disconnect all clients
                DisconnectAllClients();
                //  Close server socket
                serverSocket?.Close();
                //  Close threads
                networkThreads.ToList().ForEach(networkThread =>
                {
                    networkThread.Value.sendingWorker.Stop();
                    networkThread.Value.receivingWorker.Stop();
                    networkThread.Value.nextClientEvent.Set();
                    networkThread.Value.connectionWorker?.Join();
                    networkThreads.TryRemove(networkThread);
                });
                //  Clear dictionary
                networkThreads = new ConcurrentDictionary<int, NetworkThread>();
                clients = new ConcurrentDictionary<Socket, Client>();
            }
        }

        void ChangeNetworkStatus(NetworkStatus _networkStatus) { ChangeNetworkStatus(_networkStatus, null); }
        void ChangeNetworkStatus(NetworkStatus _networkStatus, Socket? _socket)
        {
            networkStatus = _networkStatus;
            SendNetworkStatus(_networkStatus, _socket);
        }
        void SendNetworkStatus(NetworkStatus _networkStatus) { SendNetworkStatus(_networkStatus, null); }
        void SendNetworkStatus(NetworkStatus _networkStatus, Socket? _socket)
        {
            networkStatusMethod?.Invoke(_networkStatus, _socket);
        }

        void StartListener()
        {
            try
            {
                serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                serverSocket.Bind(new IPEndPoint(IPAddress.Any, networkConfig.port));
                networkConfig.SetConfig(serverSocket);
                serverSocket.Listen(networkConfig.backLog);
                ChangeNetworkStatus(NetworkStatus.launched, serverSocket);
            }
            catch (Exception exception)
            {
                SendNetworkStatus(NetworkStatus.errorListener);
                ChangeNetworkStatus(NetworkStatus.stopped);
                //  Log to file
                //  Console.WriteLine(exception.ToString());
                return;
            }
            //  time to spawn workers :D
            for (int i = 0; i < Environment.ProcessorCount; i++)
            {
                int id = i;
                NetworkThread network = new NetworkThread();
                if (networkThreads.TryAdd(id, network))
                {
                    network.connectionWorker = new Thread(() =>
                    {
                        networkThreads[id].nextClientEvent.WaitOne();
                        while (networkStatus == NetworkStatus.launched)
                        {
                            serverSocket.BeginAccept(new AsyncCallback(AcceptConnection), networkThreads[id]);
                            networkThreads[id].nextClientEvent.WaitOne();
                        }
                    });
                    network.receivingWorker = new QueueWorker<Packet>(_packet => { decodeMethod?.Invoke(_packet); });
                    network.sendingWorker = new QueueWorker<Packet>(_packet =>
                    {
                        try
                        {
                            byte[] packetData = new byte[_packet.data.Length + 2];
                            Array.Copy(BitConverter.GetBytes((ushort)_packet.data.Length), packetData, 2);
                            Array.Copy(_packet.data, 0, packetData, 2, _packet.data.Length);
                            _packet.socket.BeginSend(packetData, 0, packetData.Length, SocketFlags.None, new AsyncCallback(
                                _asyncResult =>
                                {
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
                                    networkThreads[id].nextSendEvent.Set();
                                }
                                ), _packet.socket);
                            networkThreads[id].nextSendEvent.WaitOne();
                        }
                        catch (Exception exception)
                        {
                            SendNetworkStatus(NetworkStatus.errorSendPacket, _packet.socket);
                            //  Log to file
                            //  Console.WriteLine(exception.ToString());
                        }
                    });
                    network.connectionWorker.Start();
                    network.receivingWorker.Start();
                    network.sendingWorker.Start();
                }
            }
            UnlockNextClient();
        }
        void UnlockNextClient()
        {
            networkThreads.Values.OrderBy(x => x.connectionCount).First().nextClientEvent.Set();
        }

        void AcceptConnection(IAsyncResult _asyncResult)
        {
            UnlockNextClient();
            NetworkThread network = (NetworkThread)_asyncResult.AsyncState;
            Socket clientSocket;
            try
            {
                clientSocket = serverSocket.EndAccept(_asyncResult);
            }
            catch (Exception exception)
            {
                SendNetworkStatus(NetworkStatus.errorConnection);
                //  Log to file
                //  Console.WriteLine(exception.ToString());
                return;
            }
            if (networkStatus == NetworkStatus.launched)
                if (clients.Count <= networkConfig.maxClients)
                {
                    Client client = new Client(clientSocket, network, networkConfig.bufferSize);
                    networkConfig.SetConfig(client.socket);
                    //  Add new client
                    if (clients.TryAdd(client.socket, client))
                    {
                        network.connectionCount++;
                        connectMethod?.Invoke(client.socket);
                    }
                    client.socket.BeginReceive(client.buffer, 0, networkConfig.bufferSize, SocketFlags.None, new AsyncCallback(ReceivePacket), client);
                    return;
                }
                else
                    SendNetworkStatus(NetworkStatus.clientsLimit, clientSocket);
            Disconnect(clientSocket);
        }
        void ReceivePacket(IAsyncResult _asyncResult)
        {
            Client client = (Client)_asyncResult.AsyncState;
            int packetSize = 0;
            try
            {
                packetSize = client.socket.EndReceive(_asyncResult);
            }
            catch (Exception exception)
            {
                SendNetworkStatus(NetworkStatus.errorRecivePacket, client.socket);
                Disconnect(client.socket);
                //  Log to file
                //  Console.WriteLine(exception.ToString());
                return;
            }
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
            SendNetworkStatus(NetworkStatus.errorRecivePacket, client.socket);
            Disconnect(client.socket);
        }

        public void Send(Socket _socket, byte[] _data) { Send(new Packet(_socket, _data)); }
        public void Send(Socket _socket, List<byte> _data) { Send(_socket, _data.ToArray()); }
        public void Send(Packet _packet)
        {
            if (clients.TryGetValue(_packet.socket, out Client client))
                client.network.sendingWorker.Enqueue(_packet);
        }
        public void Disconnect(Socket _socket)
        {
            if (clients.TryRemove(_socket, out Client client))
            {
                client.network.connectionCount--;
                disconnectMethod?.Invoke(_socket);
            }
            _socket?.Close();
        }
        public void DisconnectAllClients()
        {
            clients.Keys.ToList().ForEach(socket => { Disconnect(socket); });
        }
    }
}
