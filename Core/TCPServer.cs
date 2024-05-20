using KazDev.Core;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Sockets;

namespace KazNet.Core
{
    public class TCPServer
    {
        bool isRunning = false;
        public NetworkStatus GetNetworkStatus { get => isRunning ? NetworkStatus.launched : NetworkStatus.stopped; }

        ServerNetworkConfig networkConfig;
        public string Address { get => networkConfig.address; }
        public ushort Port { get => networkConfig.port; }
        public int ConnectionCount { get => networkThreads.Values.Sum(x => x.connectionCount); }

        Socket serverSocket;
        AutoResetEvent serverEvent = new(true);
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
            serverEvent.WaitOne();
            if (!isRunning)
            {
                isRunning = true;
                SendNetworkStatus(NetworkStatus.started);
                StartListener();
            }
            serverEvent.Set();
        }
        public void Stop()
        {
            serverEvent.WaitOne();
            if (isRunning)
            {
                isRunning = false;
                //  Disconnect all clients
                DisconnectAllClients();
                //  Close threads
                networkThreads.ToList().ForEach(networkThread =>
                {
                    networkThread.Value.sendingWorker.Stop();
                    networkThread.Value.receivingWorker.Stop();
                    networkThread.Value.nextClientEvent.Set();
                });
                networkThreads.ToList().ForEach(networkThread =>
                {
                    networkThread.Value.connectionWorker.Join();
                    networkThreads.TryRemove(networkThread);
                });
                //  Close server socket
                serverSocket.Close();
                //  Clear dictionary
                networkThreads = new ConcurrentDictionary<int, NetworkThread>();
                clients = new ConcurrentDictionary<Socket, Client>();
                SendNetworkStatus(NetworkStatus.stopped);
            }
            serverEvent.Set();
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
                SendNetworkStatus(NetworkStatus.launched, serverSocket);
            }
            catch (Exception exception)
            {
                SendNetworkStatus(NetworkStatus.errorListener);
                serverEvent.Set();
                Stop();
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
                        while (isRunning)
                        {
                            serverSocket.BeginAccept(new AsyncCallback(AcceptConnection), networkThreads[id]);
                            networkThreads[id].nextClientEvent.WaitOne();
                        }
                    });
                    network.receivingWorker = new QueueWorker<Packet>(Decode);
                    network.sendingWorker = new QueueWorker<Packet>(_packet =>
                    {
                        try
                        {
                            int index = 0;
                            int dataLength = networkConfig.bufferSize - 4;
                            int packetCounter = _packet.data.Length / dataLength - ((_packet.data.Length % dataLength == 0) ? 1 : 0);
                            byte[] packetData;
                            for (int i = packetCounter; i > 0; i--)
                            {
                                packetData = new byte[networkConfig.bufferSize];
                                Array.Copy(BitConverter.GetBytes((ushort)networkConfig.bufferSize), packetData, 2);
                                Array.Copy(BitConverter.GetBytes((ushort)i), 0, packetData, 2, 2);
                                Array.Copy(_packet.data, index, packetData, 4, dataLength);
                                index += dataLength;
                                _packet.socket.BeginSend(packetData, 0, packetData.Length, SocketFlags.None, new AsyncCallback(_asyncResult =>
                                    {
                                        networkThreads[id].nextSendEvent.Set();
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
                                    ), _packet.socket);
                                networkThreads[id].nextSendEvent.WaitOne();
                            }
                            dataLength = _packet.data.Length - index;
                            packetData = new byte[dataLength + 4];
                            Array.Copy(BitConverter.GetBytes((ushort)packetData.Length), packetData, 2);
                            Array.Copy(BitConverter.GetBytes((ushort)0), 0, packetData, 2, 2);
                            Array.Copy(_packet.data, index, packetData, 4, dataLength);
                            _packet.socket.BeginSend(packetData, 0, packetData.Length, SocketFlags.None, new AsyncCallback(
                                _asyncResult =>
                                {
                                    networkThreads[id].nextSendEvent.Set();
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
                    network.sendingWorker.Start();
                    network.receivingWorker.Start();
                    network.connectionWorker.Start();
                }
            }
            UnlockNextClient();
        }
        void UnlockNextClient()
        {
            networkThreads.Values.OrderBy(x => x.connectionCount).FirstOrDefault()?.nextClientEvent.Set();
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
            if (isRunning)
                if (clients.Count < networkConfig.maxClients)
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
                Disconnect(client.socket);
                //  Log to file
                //  Console.WriteLine(exception.ToString());
            }
        }
        void Decode(Packet _packet) { decodeMethod?.Invoke(_packet); }

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
            _socket.Close();
        }
        public void DisconnectAllClients()
        {
            clients.Keys.ToList().ForEach(socket => { Disconnect(socket); });
        }
    }
}
