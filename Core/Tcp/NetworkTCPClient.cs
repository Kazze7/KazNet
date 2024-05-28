using KazDev.Core;
using KazNet.Core;
using System.Collections.Concurrent;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace KazNet.Tcp
{
    public abstract class NetworkTCPClient : NetworkClient
    {
        bool isRunning = false;
        AutoResetEvent clientEvent = new(true);
        NetworkTCPClientConfig networkConfig;
        ConnectionTCPClient connection;

        NetworkMessageHandlerList messageHandlerList = new();
        ConcurrentDictionary<NetworkMessageThread, QueueWorker<NetworkMessageThreadOnClient>> messageThreads = new();

        public bool IsRunning { get { return isRunning; } }
        public string Address { get => networkConfig.address; }
        public ushort Port { get => networkConfig.port; }

        public NetworkTCPClient(NetworkTCPServerConfig _networkConfig, NetworkMessageHandlerList _messageHandlerList)
        {
            networkConfig = _networkConfig;
            messageHandlerList = _messageHandlerList;
        }

        public abstract void OnConnected();
        public abstract void OnDisconnected();
        public virtual void OnStatusChange(NetworkStatus _status) { }
        public virtual void OnError(NetworkError _error, string _exception) { }

        public void Start()
        {
            clientEvent.WaitOne();
            if (!isRunning)
            {
                isRunning = true;
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
                connection.connectionThreads.Stop();
                messageThreads.ToList().ForEach(messageThread =>
                {
                    messageThread.Value.Stop();
                    messageThreads.TryRemove(messageThread);
                });
                //  Close client socket
                connection.Close();
                //  Clear dictionary
                messageThreads = new();
                OnStatusChange(NetworkStatus.stopped);
            }
            clientEvent.Set();
        }
        void StartConnection()
        {
            OnStatusChange(NetworkStatus.started);
            try
            {
                connection = new ConnectionTCPClient(new TcpClient(), new ConnectionClientThreads(), networkConfig.bufferSize);
            }
            catch (Exception exception)
            {
                OnError(NetworkError.errorListener, exception.ToString());
                Stop();
                return;
            }
            connection.connectionThreads.connectionWorker = new Thread(() =>
            {
                connection.tcpClient.BeginConnect(networkConfig.IPAddress, networkConfig.port, new AsyncCallback(AcceptConnection), connection);
            });
            connection.connectionThreads.receivingWorker = new QueueWorker<NetworkClientMessage>(DecodeMessage);
            connection.connectionThreads.sendingWorker = new QueueWorker<NetworkClientMessage>(SendMessage);
            connection.connectionThreads.Start();
        }
        void AcceptConnection(IAsyncResult _asyncResult)
        {
            ConnectionTCPClient connection = (ConnectionTCPClient)_asyncResult.AsyncState;
            try
            {
                connection.tcpClient.EndConnect(_asyncResult);
                networkConfig.SetConfig(connection.tcpClient);
                connection.stream = connection.tcpClient.GetStream();
                //  Ssl stream
                if (networkConfig.useSsl)
                {
                    connection.stream = new SslStream(connection.tcpClient.GetStream(), false, new RemoteCertificateValidationCallback(ValidateServerCertificate), null);
                    ((SslStream)connection.stream).BeginAuthenticateAsClient(networkConfig.sslTargetHost, null, false, new AsyncCallback(AuthenticateAsClient), connection);
                    return;
                }
                //
                NewConnection(connection);
            }
            catch (Exception exception)
            {
                OnError(NetworkError.errorConnection, exception.ToString());
                Stop();
            }
        }
        void AuthenticateAsClient(IAsyncResult _asyncResult)
        {
            ConnectionTCPClient connection = (ConnectionTCPClient)_asyncResult.AsyncState;
            try
            {
                ((SslStream)connection.stream).EndAuthenticateAsServer(_asyncResult);
            }
            catch (Exception exception)
            {
                OnError(NetworkError.errorConnection, exception.ToString());
                return;
            }
            NewConnection(connection);
        }
        void NewConnection(ConnectionTCPClient _connection)
        {
            OnStatusChange(NetworkStatus.connected);
            OnConnected();
            _connection.stream.BeginRead(_connection.buffer, 0, networkConfig.bufferSize, new AsyncCallback(ReadStream), _connection);
        }
        void ReadStream(IAsyncResult _asyncResult)
        {
            ConnectionTCPClient connection = (ConnectionTCPClient)_asyncResult.AsyncState;
            try
            {
                int packetSize = connection.stream.EndRead(_asyncResult);
                if (packetSize > 0)
                {
                    connection.data.AddRange(connection.buffer.Take(packetSize).ToArray());
                    int packetLength = BitConverter.ToInt32(connection.data.Take(4).ToArray());
                    while (packetLength <= connection.data.Count)
                    {
                        connection.connectionThreads.receivingWorker.Enqueue(new NetworkClientMessage(BitConverter.ToUInt16(connection.data.Skip(4).Take(2).ToArray()), connection.data.Skip(6).Take(packetLength - 6).ToArray()));
                        connection.data = connection.data.Skip(packetLength).ToList();
                    }
                    connection.stream.BeginRead(connection.buffer, 0, networkConfig.bufferSize, new AsyncCallback(ReadStream), connection);
                }
            }
            catch (Exception exception)
            {
                OnError(NetworkError.errorReadStream, exception.ToString());
                Disconnect();
            }
        }
        void DecodeMessage(NetworkClientMessage _networkMessage)
        {
            if (messageHandlerList.GetMessageHandler(_networkMessage.handlerId, out NetworkMessageHandler messageHandler))
                if (messageHandler != null)
                {
                    //  QueueThread
                    if (messageHandler.thread != null)
                    {
                        if (!messageThreads.TryGetValue(messageHandler.thread, out QueueWorker<NetworkMessageThreadOnClient> queueWorker))
                            queueWorker.Enqueue(new NetworkMessageThreadOnClient(messageHandler, _networkMessage));
                        else
                        {
                            queueWorker = new QueueWorker<NetworkMessageThreadOnClient>(DecodeMessage);
                            messageThreads.TryAdd(messageHandler.thread, queueWorker);
                            queueWorker.Start();
                        }
                        queueWorker.Enqueue(new NetworkMessageThreadOnClient(messageHandler, _networkMessage));
                    }
                    //  Decode
                    messageHandler.OnClient(this, _networkMessage);
                }
        }
        void DecodeMessage(NetworkMessageThreadOnClient _networkThread) { _networkThread.handler.OnClient(this, _networkThread.message); }
        void SendMessage(NetworkClientMessage _networkMessage)
        {
            try
            {
                connection.stream.Write(BitConverter.GetBytes(6 + _networkMessage.data.Length).Concat(BitConverter.GetBytes(_networkMessage.handlerId)).Concat(_networkMessage.data).ToArray());
            }
            catch (Exception exception)
            {
                OnError(NetworkError.errorWriteStream, exception.ToString());
            }
        }
        public override void SendMessage(NetworkMessageHandler _messageHandler, INetworkMessage _networkMessage)
        {
            if (messageHandlerList.GetMessageHandlerId(_messageHandler, out ushort _id))
                connection.connectionThreads.sendingWorker.Enqueue(new NetworkClientMessage(_id, _networkMessage.Serialize()));
        }
        public void Disconnect()
        {
            OnDisconnected();
            Stop();
        }
        bool ValidateServerCertificate(object _sender, X509Certificate _certificate, X509Chain _chain, SslPolicyErrors _sslPolicyErrors)
        {
            if (_sslPolicyErrors == SslPolicyErrors.None)
                return true;
            if (SslPolicyErrors.RemoteCertificateChainErrors == _sslPolicyErrors)
            {
                OnError(NetworkError.remoteCertificateChainErrors, _sslPolicyErrors.ToString());
                return true;
            }
            OnError(NetworkError.invalidServerCertificate, _sslPolicyErrors.ToString());
            return false;
        }
    }
}
