using KazDev.Core;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace KazNet.Core
{
    public class TCPClient
    {
        bool isRunning = false;
        AutoResetEvent clientEvent = new(true);
        NetworkConfig networkConfig;
        Client client;

        public bool IsRunning { get { return isRunning; } }
        public string Address { get => networkConfig.address; }
        public ushort Port { get => networkConfig.port; }

        public delegate void NetworkStatusMethod(NetworkStatus _networkStatus, string? _exception);
        NetworkStatusMethod networkStatusMethod;
        public delegate void ConnectMethod();
        ConnectMethod connectMethod;
        public delegate void DisconnectMethod();
        DisconnectMethod disconnectMethod;
        public delegate void DecodeMethod(byte[] _data);
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
                client.networkThread.Stop();
                //  Close client socket
                client.tcpClient.Close();
                SendNetworkStatus(NetworkStatus.stopped);
            }
            clientEvent.Set();
        }
        void StartConnection()
        {
            SendNetworkStatus(NetworkStatus.started);
            try
            {
                client = new Client(new TcpClient(), new NetworkThread(), networkConfig.bufferSize);
            }
            catch (Exception exception)
            {
                SendNetworkStatus(NetworkStatus.errorListener, exception.ToString());
                Stop();
                return;
            }
            client.networkThread.connectionWorker = new Thread(() =>
            {
                client.tcpClient.BeginConnect(networkConfig.IPAddress, networkConfig.port, new AsyncCallback(AcceptConnection), client);
            });
            client.networkThread.receivingWorker = new QueueWorker<NetworkPacket>(Decode);
            client.networkThread.sendingWorker = new QueueWorker<NetworkPacket>(SendStream);
            client.networkThread.Start();
        }
        void AcceptConnection(IAsyncResult _asyncResult)
        {
            try
            {
                Client client = (Client)_asyncResult.AsyncState;
                client.tcpClient.EndConnect(_asyncResult);
                networkConfig.SetConfig(client.tcpClient);
                //  Ssl stream
                if (networkConfig.useSsl)
                {
                    SslStream sslStream = new SslStream(client.tcpClient.GetStream(), false, new RemoteCertificateValidationCallback(ValidateServerCertificate), null);
                    sslStream.AuthenticateAsClient(networkConfig.sslTargetHost);
                    client.stream = sslStream;
                }
                else
                    client.stream = (NetworkStream)client.tcpClient.GetStream();
                //
                SendNetworkStatus(NetworkStatus.connected);
                connectMethod?.Invoke();
                //
                client.stream.BeginRead(client.buffer, 0, networkConfig.bufferSize, new AsyncCallback(ReadStream), client);
            }
            catch (Exception exception)
            {
                SendNetworkStatus(NetworkStatus.errorConnection, exception.ToString());
                Stop();
            }
        }
        void ReadStream(IAsyncResult _asyncResult)
        {
            try
            {
                Client client = (Client)_asyncResult.AsyncState;
                int packetSize = client.stream.EndRead(_asyncResult);
                if (packetSize > 0)
                {
                    client.data.AddRange(client.buffer.Take(packetSize).ToArray());
                    int packetLength = BitConverter.ToInt32(client.data.Take(4).ToArray());
                    while (packetLength <= client.data.Count)
                    {
                        client.networkThread.receivingWorker.Enqueue(new NetworkPacket(client.tcpClient, client.data.Skip(4).Take(packetLength - 4).ToArray()));
                        client.data = client.data.Skip(packetLength).ToList();
                    }
                    client.stream.BeginRead(client.buffer, 0, networkConfig.bufferSize, new AsyncCallback(ReadStream), client);
                }
            }
            catch (Exception exception)
            {
                SendNetworkStatus(NetworkStatus.errorReadPacket, exception.ToString());
                Disconnect();
            }
        }
        void SendStream(NetworkPacket _packet)
        {
            try
            {
                client.stream.Write(BitConverter.GetBytes(4 + _packet.data.Length).Concat(_packet.data).ToArray());
            }
            catch (Exception exception)
            {
                SendNetworkStatus(NetworkStatus.errorSendPacket, exception.ToString());
            }
        }
        void SendNetworkStatus(NetworkStatus _networkStatus) { SendNetworkStatus(_networkStatus, null); }
        void SendNetworkStatus(NetworkStatus _networkStatus, string? _exception) { networkStatusMethod?.Invoke(_networkStatus, _exception); }
        void Decode(NetworkPacket _networkPacket) { decodeMethod?.Invoke(_networkPacket.data); }
        public void Send(byte[] _data) { Send(new NetworkPacket(client.tcpClient, _data)); }
        public void Send(List<byte> _data) { Send(new NetworkPacket(client.tcpClient, _data)); }
        void Send(NetworkPacket _networkPacket) { _networkPacket.tcpClient = client.tcpClient; client.networkThread.sendingWorker.Enqueue(_networkPacket); }
        public void Disconnect() { disconnectMethod?.Invoke(); Stop(); }
        bool ValidateServerCertificate(object _sender, X509Certificate _certificate, X509Chain _chain, SslPolicyErrors _sslPolicyErrors)
        {
            if (_sslPolicyErrors == SslPolicyErrors.None)
                return true;
            if (SslPolicyErrors.RemoteCertificateChainErrors == _sslPolicyErrors)
            {
                SendNetworkStatus(NetworkStatus.remoteCertificateChainErrors, _sslPolicyErrors.ToString());
                return true;
            }
            SendNetworkStatus(NetworkStatus.invalidServerCertificate, _sslPolicyErrors.ToString());
            return false;
        }
    }
}
