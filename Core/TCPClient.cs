using KazDev.Core;
using System.Net;
using System.Net.Sockets;

namespace KazNet.Core
{
    public class TCPClient
    {
        TCPClientConfig clientConfig;
        public string Address { get => clientConfig.address; }
        public ushort Port { get => clientConfig.port; }

        NetworkStatus networkStatus = NetworkStatus.stopped;
        public NetworkStatus GetNetworkStatus { get => networkStatus; }

        Socket clientSocket;
        ClientThread clientThread;
        public byte[] buffer;

        public delegate void NetworkStatusMethod(NetworkStatus _networkStatus, Socket? _socket);
        NetworkStatusMethod networkStatusMethod;
        public delegate void ConnectMethod(Socket _socket);
        ConnectMethod connectMethod;
        public delegate void DisconnectMethod(Socket _socket);
        DisconnectMethod disconnectMethod;
        public delegate void DecodeMethod(Packet _packet);
        DecodeMethod decodeMethod;

        public TCPClient(
            TCPServerConfig _clientConfig,
            NetworkStatusMethod _networkStatus = null,
            ConnectMethod _connect = null,
            DisconnectMethod _disconnect = null,
            DecodeMethod _decodePacket = null
            )
        {
            clientConfig = _clientConfig;
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
                StartConnection();
            }
        }
        public void Stop()
        {
            if (networkStatus != NetworkStatus.started)
            {
                ChangeNetworkStatus(NetworkStatus.stopped);
                //  Close client socket
                clientSocket?.Close();
                //  Close threads
                clientThread.sendingWorker.Stop();
                clientThread.receivingWorker.Stop();
                clientThread.connectionThread?.Join();
                //  Clear dictionary
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

        void StartConnection()
        {
            IPAddress ipAddress;
            try
            {
                if (!IPAddress.TryParse(clientConfig.address, out ipAddress))
                {
                    IPHostEntry ipHostInfo = Dns.GetHostEntry(clientConfig.address);
                    ipAddress = ipHostInfo.AddressList.Where(x => x.AddressFamily == AddressFamily.InterNetwork).FirstOrDefault();
                }
                clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            }
            catch (Exception exception)
            {
                SendNetworkStatus(NetworkStatus.errorConnection);
                ChangeNetworkStatus(NetworkStatus.stopped);
                //  Log to file
                //  Console.WriteLine(exception.ToString());
                return;
            }
            clientThread = new ClientThread();
            clientThread.connectionThread = new Thread(() =>
            {
                clientSocket.BeginConnect(new IPEndPoint(ipAddress, clientConfig.port), new AsyncCallback(AcceptConnection), clientSocket);
            });
            clientThread.receivingWorker = new QueueWorker<Packet>(_packet => { decodeMethod?.Invoke(_packet); });
            clientThread.sendingWorker = new QueueWorker<Packet>(_packet =>
            {
                SendPacket(_packet);
            });
            clientThread.connectionThread.Start();
            clientThread.receivingWorker.Start();
            clientThread.sendingWorker.Start();
        }
        void AcceptConnection(IAsyncResult _asyncResult)
        {
            try
            {
                ((Socket)_asyncResult.AsyncState).EndConnect(_asyncResult);
                clientConfig.SetClientConfig(clientSocket);
                ChangeNetworkStatus(NetworkStatus.connected);
                connectMethod?.Invoke(clientSocket);
                buffer = new byte[clientConfig.bufferSize];
                clientSocket.BeginReceive(buffer, 0, clientConfig.bufferSize, SocketFlags.None, new AsyncCallback(ReceivePacket), clientSocket);
            }
            catch (Exception exception)
            {
                SendNetworkStatus(NetworkStatus.errorConnection, clientSocket);
                Stop();
                //  Log to file
                //  Console.WriteLine(exception.ToString());
            }
        }
        void ReceivePacket(IAsyncResult _asyncResult)
        {
            int packetSize = 0;
            try
            {
                packetSize = ((Socket)_asyncResult.AsyncState).EndReceive(_asyncResult);
            }
            catch (Exception exception)
            {
                SendNetworkStatus(NetworkStatus.errorRecivePacket, clientSocket);
                Disconnect();
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
                    packetLength = BitConverter.ToUInt16(buffer, index);
                    index += 2;
                    packetData = new byte[packetLength];
                    Array.Copy(buffer, index, packetData, 0, packetLength);
                    clientThread.receivingWorker.Enqueue(new Packet(clientSocket, packetData));
                    index += packetLength;
                }
                while (index < packetSize);
                clientSocket.BeginReceive(buffer, 0, clientConfig.bufferSize, SocketFlags.None, new AsyncCallback(ReceivePacket), clientSocket);
                return;
            }
            SendNetworkStatus(NetworkStatus.errorRecivePacket, clientSocket);
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
                clientThread.nextSendEvent.WaitOne();
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
            clientThread.nextSendEvent.Set();
        }

        public void Send(byte[] _data) { Send(new Packet(clientSocket, _data)); }
        public void Send(List<byte> _data) { Send(new Packet(clientSocket, _data.ToArray())); }
        public void Send(Packet _packet)
        {
            clientThread.sendingWorker.Enqueue(_packet);
        }
        public void Disconnect()
        {
            disconnectMethod?.Invoke(clientSocket);
            clientSocket?.Close();
            Stop();
        }
    }
}
