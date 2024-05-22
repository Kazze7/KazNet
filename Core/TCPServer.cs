using KazDev.Core;
using System.Collections.Concurrent;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace KazNet.Core
{
    public class TCPServer
    {
        bool isRunning = false;
        AutoResetEvent serverEvent = new(true);
        ServerNetworkConfig networkConfig;
        TcpListener server;

        ConcurrentDictionary<int, NetworkThread> networkThreads = new();
        ConcurrentDictionary<TcpClient, Client> clients = new();

        public bool IsRunning { get { return isRunning; } }
        public string Address { get => networkConfig.address; }
        public ushort Port { get => networkConfig.port; }
        public int ConnectionCount { get => networkThreads.Values.Sum(x => x.connectionCount); }

        public delegate void NetworkStatusMethod(NetworkStatus _networkStatus, TcpClient? _tcpClient, string? _exception);
        NetworkStatusMethod networkStatusMethod;
        public delegate void ConnectMethod(TcpClient _tcpClient);
        ConnectMethod connectMethod;
        public delegate void DisconnectMethod(TcpClient _tcpClient);
        DisconnectMethod disconnectMethod;
        public delegate void DecodeMethod(NetworkPacket _networkPacket);
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
                    networkThread.Value.Stop();
                    networkThreads.TryRemove(networkThread);
                });
                //  Close server socket
                server.Stop();
                //  Clear dictionary
                networkThreads = new();
                clients = new();
                SendNetworkStatus(NetworkStatus.stopped);
            }
            serverEvent.Set();
        }

        void StartListener()
        {
            SendNetworkStatus(NetworkStatus.started);
            try
            {
                server = new TcpListener(networkConfig.IPAddress, networkConfig.port);
                networkConfig.SetConfig(server);
                server.Start(networkConfig.backLog);
                SendNetworkStatus(NetworkStatus.launched);
            }
            catch (Exception exception)
            {
                SendNetworkStatus(NetworkStatus.errorListener, exception.ToString());
                serverEvent.Set();
                Stop();
                return;
            }
            //  time to spawn workers :D
            for (int i = 0; i < Environment.ProcessorCount; i++)
            {
                int id = i;
                NetworkThread networkThread = new NetworkThread();
                if (networkThreads.TryAdd(id, networkThread))
                {
                    networkThread.connectionWorker = new Thread(() =>
                    {
                        networkThreads[id].nextConnectionEvent.WaitOne();
                        while (isRunning)
                        {
                            server.BeginAcceptTcpClient(new AsyncCallback(AcceptConnection), networkThreads[id]);
                            networkThreads[id].nextConnectionEvent.WaitOne();
                        }
                    });
                    networkThread.receivingWorker = new QueueWorker<NetworkPacket>(Decode);
                    networkThread.sendingWorker = new QueueWorker<NetworkPacket>(SendStream);
                    networkThread.Start();
                }
            }
            UnlockNextConnection();
        }
        void UnlockNextConnection() { networkThreads.Values.OrderBy(x => x.connectionCount).FirstOrDefault()?.nextConnectionEvent.Set(); }
        void AcceptConnection(IAsyncResult _asyncResult)
        {
            UnlockNextConnection();
            NetworkThread networkThread = (NetworkThread)_asyncResult.AsyncState;
            TcpClient tcpClient;
            try
            {
                tcpClient = server.EndAcceptTcpClient(_asyncResult);
            }
            catch (Exception exception)
            {
                SendNetworkStatus(NetworkStatus.errorConnection, exception.ToString());
                return;
            }
            if (IsRunning)
                if (clients.Count < networkConfig.maxConnections)
                {
                    networkConfig.SetConfig(tcpClient);
                    Client client = new Client(tcpClient, networkThread, networkConfig.bufferSize);
                    //  Add new client
                    if (clients.TryAdd(client.tcpClient, client))
                    {
                        networkThread.connectionCount++;
                        connectMethod?.Invoke(client.tcpClient);
                    }
                    //  Ssl stream
                    if (networkConfig.useSsl)
                    {
                        SslStream sslStream = new SslStream(client.tcpClient.GetStream(), false);
                        sslStream.AuthenticateAsServer(new X509Certificate2(networkConfig.sslFilePathPfx, networkConfig.sslFilePassword), false, true);
                        client.Stream = sslStream;
                    }
                    else
                        client.Stream = (NetworkStream)client.tcpClient.GetStream();
                    client.streamEvent.Set();
                    //
                    client.Stream.BeginRead(client.buffer, 0, networkConfig.bufferSize, new AsyncCallback(ReadStream), client);
                    return;
                }
                else
                    SendNetworkStatus(NetworkStatus.connectionsLimit, tcpClient);
            Disconnect(tcpClient);
        }
        void ReadStream(IAsyncResult _asyncResult)
        {
            Client client = (Client)_asyncResult.AsyncState;
            try
            {
                int packetSize = client.Stream.EndRead(_asyncResult);
                if (packetSize > 0)
                {
                    client.data.AddRange(client.buffer.Take(packetSize).ToArray());
                    int packetLength = BitConverter.ToInt32(client.data.Take(4).ToArray());
                    while (packetLength <= client.data.Count)
                    {
                        client.networkThread.receivingWorker.Enqueue(new NetworkPacket(client.tcpClient, client.data.Skip(4).Take(packetLength - 4).ToArray()));
                        client.data = client.data.Skip(packetLength).ToList();
                    }
                    client.Stream.BeginRead(client.buffer, 0, networkConfig.bufferSize, new AsyncCallback(ReadStream), client);
                }
            }
            catch (Exception exception)
            {
                SendNetworkStatus(NetworkStatus.errorReadPacket, client.tcpClient, exception.ToString());
                Disconnect(client.tcpClient);
            }
        }
        void SendStream(NetworkPacket _networkPacket)
        {
            if (clients.TryGetValue(_networkPacket.tcpClient, out Client client))
                try
                {
                    client.Stream.Write(BitConverter.GetBytes(4 + _networkPacket.data.Length).Concat(_networkPacket.data).ToArray());
                }
                catch (Exception exception)
                {
                    SendNetworkStatus(NetworkStatus.errorSendPacket, client.tcpClient, exception.ToString());

                }
        }
        void SendNetworkStatus(NetworkStatus _networkStatus) { SendNetworkStatus(_networkStatus, null, null); }
        void SendNetworkStatus(NetworkStatus _networkStatus, TcpClient? _tcpClient) { SendNetworkStatus(_networkStatus, _tcpClient, null); }
        void SendNetworkStatus(NetworkStatus _networkStatus, string _exception) { SendNetworkStatus(_networkStatus, null, _exception); }
        void SendNetworkStatus(NetworkStatus _networkStatus, TcpClient? _tcpClient, string? _exception) { networkStatusMethod?.Invoke(_networkStatus, _tcpClient, _exception); }
        void Decode(NetworkPacket _networkPacket) { decodeMethod?.Invoke(_networkPacket); }
        public void Send(TcpClient _tcpClient, byte[] _data) { Send(new NetworkPacket(_tcpClient, _data)); }
        public void Send(TcpClient _tcpClient, List<byte> _data) { Send(new NetworkPacket(_tcpClient, _data)); }
        public void Send(NetworkPacket _networkPacket)
        {
            if (clients.TryGetValue(_networkPacket.tcpClient, out Client client))
                client.networkThread.sendingWorker.Enqueue(_networkPacket);
        }
        public void Disconnect(TcpClient _tcpClient)
        {
            if (clients.TryRemove(_tcpClient, out Client client))
            {
                client.networkThread.connectionCount--;
                disconnectMethod?.Invoke(_tcpClient);
            }
            _tcpClient.Close();
        }
        public void DisconnectAllClients() { clients.Keys.ToList().ForEach(client => { Disconnect(client); }); }
    }
}
