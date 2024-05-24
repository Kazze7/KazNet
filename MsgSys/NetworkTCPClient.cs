using KazDev.Core;
using KazNet.Core;
using System.Collections.Concurrent;

namespace KazNet.MsgSys
{
    public abstract class NetworkTCPClient : Core.NetworkTCPClient
    {
        ConcurrentDictionary<NetworkThread, QueueWorker<NetworkThreadData>> networkThreads = new();
        public NetworkMessageHandlerList messageHandlerList = new();

        public NetworkTCPClient(NetworkConfig _networkConfig) : base(_networkConfig) { }

        public override void Stop()
        {
            base.Stop();
            networkThreads.ToList().ForEach(networkThread =>
            {
                networkThread.Value.Stop();
                networkThreads.TryRemove(networkThread);
            });
            networkThreads = new();
        }

        public override void Decode(byte[] _data)
        {
            short messageId = BitConverter.ToInt16(_data.Take(2).ToArray());
            if (messageHandlerList.GetMessageHandler(messageId, out NetworkMessageHandler messageHandler))
                if (messageHandler != null)
                {
                    _data = _data.Skip(2).ToArray();
                    NetworkMessage message = new NetworkMessage(null, messageId, _data);
                    //  QueueThread
                    if (messageHandler.thread != null)
                    {
                        if (!networkThreads.TryGetValue(messageHandler.thread, out QueueWorker<NetworkThreadData> queueWorker))
                            queueWorker.Enqueue(new NetworkThreadData(messageHandler, message));
                        else
                        {
                            queueWorker = new QueueWorker<NetworkThreadData>(Decode);
                            networkThreads.TryAdd(messageHandler.thread, queueWorker);
                            queueWorker.Start();
                        }
                        queueWorker.Enqueue(new NetworkThreadData(messageHandler, message));
                    }
                    //  Decode
                    messageHandler.OnClient(this, message);
                }
        }
        void Decode(NetworkThreadData _networkThreadData) { _networkThreadData.messageHandler.OnClient(this, _networkThreadData.message); }
    }
}
