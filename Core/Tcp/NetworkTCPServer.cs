using KazDev.Core;
using KazDev.UniqueID;
using KazNet.Core;
using System.Collections.Concurrent;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace KazNet.Tcp
{
    public abstract class NetworkTCPServer : NetworkServer
    {
        bool isRunning = false;
        AutoResetEvent serverEvent = new(true);
        NetworkTCPServerConfig networkConfig;
        TcpListener listener;

        ConcurrentDictionary<int, ConnectionServerThreads> connectionThreadsDictionary = new();
        ConcurrentDictionary<ulong, ConnectionTCPServer> connections = new();

        MUIDGenerator UIDGenerator = new MUIDGenerator();
        NetworkMessageHandlerList messageHandlerList = new();
        NetworkMessagePermissionGroup newConnectionPermissionGroup = new();
        ConcurrentDictionary<NetworkMessageThread, QueueWorker<NetworkMessageThreadOnServer>> messageThreads = new();

        public bool IsRunning { get { return isRunning; } }
        public string Address { get => networkConfig.address; }
        public ushort Port { get => networkConfig.port; }
        public int ConnectionCount { get => connections.Count; }

        public NetworkTCPServer(NetworkTCPServerConfig _networkConfig, NetworkMessageHandlerList _messageHandlerList, NetworkMessagePermissionGroup _newConnectionPermissionGroup)
        {
            networkConfig = _networkConfig;
            messageHandlerList = _messageHandlerList;
            newConnectionPermissionGroup = _newConnectionPermissionGroup;
        }

        public abstract void OnConnected(ClientEntity _clientData);
        public abstract void OnDisconnected(ClientEntity _clientData);
        public virtual void OnStatusChange(NetworkStatus _status) { }
        public virtual void OnError(NetworkError _error, ClientEntity _clientData, string _exception) { }

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
                DisconnectAll();
                //  Close threads
                connectionThreadsDictionary.ToList().ForEach(connectionThreads =>
                {
                    connectionThreads.Value.Stop();
                    connectionThreadsDictionary.TryRemove(connectionThreads);
                });
                messageThreads.ToList().ForEach(messageThread =>
                {
                    messageThread.Value.Stop();
                    messageThreads.TryRemove(messageThread);
                });
                //  Close server socket
                listener.Stop();
                //  Clear dictionary
                connectionThreadsDictionary = new();
                connections = new();
                messageThreads = new();
                OnStatusChange(NetworkStatus.stopped);
            }
            serverEvent.Set();
        }

        void StartListener()
        {
            OnStatusChange(NetworkStatus.started);
            try
            {
                listener = new TcpListener(networkConfig.IPAddress, networkConfig.port);
                //networkConfig.SetConfig(server);
                listener.Start(networkConfig.backLog);
                OnStatusChange(NetworkStatus.launched);
            }
            catch (Exception exception)
            {
                OnError(NetworkError.errorListener, null, exception.ToString());
                serverEvent.Set();
                Stop();
                return;
            }
            //  time to spawn workers :D
            for (int i = 0; i < Environment.ProcessorCount; i++)
            {
                int id = i;
                ConnectionServerThreads connectionThreads = new ConnectionServerThreads();
                if (connectionThreadsDictionary.TryAdd(id, connectionThreads))
                {
                    connectionThreads.connectionWorker = new Thread(() =>
                    {
                        connectionThreadsDictionary[id].connectionEvent.WaitOne();
                        while (isRunning)
                        {
                            listener.BeginAcceptTcpClient(new AsyncCallback(AcceptConnection), connectionThreadsDictionary[id]);
                            connectionThreadsDictionary[id].connectionEvent.WaitOne();
                        }
                    });
                    connectionThreads.receivingWorker = new QueueWorker<NetworkServerMessage>(DecodeMessage);
                    connectionThreads.sendingWorker = new QueueWorker<NetworkServerMessage>(SendMessage);
                    connectionThreads.Start();
                }
            }
            UnlockNextConnection();
        }
        void UnlockNextConnection() { connectionThreadsDictionary.Values.OrderBy(x => x.connectionCount).FirstOrDefault()?.connectionEvent.Set(); }
        void AcceptConnection(IAsyncResult _asyncResult)
        {
            UnlockNextConnection();
            ConnectionServerThreads connectionThreads = (ConnectionServerThreads)_asyncResult.AsyncState;
            TcpClient tcpClient;
            try
            {
                tcpClient = listener.EndAcceptTcpClient(_asyncResult);
                if (IsRunning)
                    if (connections.Count < networkConfig.maxConnections)
                    {
                        networkConfig.SetConfig(tcpClient);
                        //  Ssl stream
                        Stream stream;
                        if (networkConfig.useSsl)
                        {
                            SslStream sslStream = new SslStream(tcpClient.GetStream(), false);
                            sslStream.AuthenticateAsServer(new X509Certificate2(networkConfig.sslFilePathPfx, networkConfig.sslFilePassword), false, true);
                            stream = sslStream;
                        }
                        else
                            stream = (NetworkStream)tcpClient.GetStream();
                        //  Add new client
                        ClientEntity networkClient = new ClientEntity(UIDGenerator.NewID().ToUlong(), newConnectionPermissionGroup);
                        ConnectionTCPServer connection = new ConnectionTCPServer(tcpClient, stream, networkClient, connectionThreads, networkConfig.bufferSize);
                        if (connections.TryAdd(networkClient.UID, connection))
                        {
                            connectionThreads.connectionCount++;
                            OnConnected(networkClient);
                        }
                        //
                        connection.stream.BeginRead(connection.buffer, 0, networkConfig.bufferSize, new AsyncCallback(ReadStream), connection);
                        return;
                    }
                    else
                        OnStatusChange(NetworkStatus.connectionLimit);
            }
            catch (Exception exception)
            {
                OnError(NetworkError.errorConnection, null, exception.ToString());
                return;
            }
            tcpClient?.Close();
        }
        void ReadStream(IAsyncResult _asyncResult)
        {
            ConnectionTCPServer connection = (ConnectionTCPServer)_asyncResult.AsyncState;
            try
            {
                int packetSize = connection.stream.EndRead(_asyncResult);
                if (packetSize > 0)
                {
                    connection.data.AddRange(connection.buffer.Take(packetSize).ToArray());
                    int packetLength = BitConverter.ToInt32(connection.data.Take(4).ToArray());
                    while (packetLength <= connection.data.Count)
                    {
                        connection.connectionThreads.receivingWorker.Enqueue(new NetworkServerMessage(connection.networkClient, BitConverter.ToUInt16(connection.data.Skip(4).Take(2).ToArray()), connection.data.Skip(6).Take(packetLength - 6).ToArray()));
                        connection.data = connection.data.Skip(packetLength).ToList();
                    }
                    connection.stream.BeginRead(connection.buffer, 0, networkConfig.bufferSize, new AsyncCallback(ReadStream), connection);
                }
            }
            catch (Exception exception)
            {
                OnError(NetworkError.errorReadStream, connection.networkClient, exception.ToString());
                Disconnect(connection.networkClient);
            }
        }
        void DecodeMessage(NetworkServerMessage _networkMessage)
        {
            if (messageHandlerList.GetMessageHandler(_networkMessage.handlerId, out NetworkMessageHandler messageHandler))
                if (messageHandler != null)
                {
                    //  Check permission
                    if (messageHandler.permission == null || _networkMessage.client.permissionGroup == null)
                        return;
                    if (!_networkMessage.client.permissionGroup.CheckPermission(messageHandler.permission))
                        return;
                    //  QueueThread
                    if (messageHandler.thread != null)
                    {
                        if (!messageThreads.TryGetValue(messageHandler.thread, out QueueWorker<NetworkMessageThreadOnServer> queueWorker))
                            queueWorker.Enqueue(new NetworkMessageThreadOnServer(messageHandler, _networkMessage));
                        else
                        {
                            queueWorker = new QueueWorker<NetworkMessageThreadOnServer>(DecodeMessage);
                            messageThreads.TryAdd(messageHandler.thread, queueWorker);
                            queueWorker.Start();
                        }
                        queueWorker.Enqueue(new NetworkMessageThreadOnServer(messageHandler, _networkMessage));
                    }
                    //  Decode
                    messageHandler.OnServer(this, _networkMessage);
                }
        }
        void DecodeMessage(NetworkMessageThreadOnServer _networkThread) { _networkThread.handler.OnServer(this, _networkThread.message); }
        void SendMessage(NetworkServerMessage _networkMessage)
        {
            if (connections.TryGetValue(_networkMessage.client.UID, out ConnectionTCPServer connection))
                try
                {
                    connection.stream.Write(BitConverter.GetBytes(6 + _networkMessage.data.Length).Concat(BitConverter.GetBytes(_networkMessage.handlerId)).Concat(_networkMessage.data).ToArray());
                }
                catch (Exception exception)
                {
                    OnError(NetworkError.errorWriteStream, connection.networkClient, exception.ToString());
                }
        }
        public override void SendMessage(ClientEntity _networkClient, NetworkMessageHandler _messageHandler, INetworkMessage _networkMessage)
        {
            if (connections.TryGetValue(_networkClient.UID, out ConnectionTCPServer connection))
                if (messageHandlerList.GetMessageHandlerId(_messageHandler, out ushort _id))
                    connection.connectionThreads.sendingWorker.Enqueue(new NetworkServerMessage(_networkClient, _id, _networkMessage.Serialize()));
        }
        public void Disconnect(ClientEntity _networkClient)
        {
            if (connections.TryRemove(_networkClient.UID, out ConnectionTCPServer connection))
            {
                connection.connectionThreads.connectionCount--;
                connection.Close();
                OnDisconnected(_networkClient);
            }
        }
        public void DisconnectAll() { connections.Values.ToList().ForEach(connection => { Disconnect(connection.networkClient); }); }
    }
}
