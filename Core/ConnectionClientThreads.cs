using KazDev.Core;

namespace KazNet.Core
{
    class ConnectionClientThreads
    {
        public Thread connectionWorker;
        public QueueWorker<NetworkClientMessage> receivingWorker;
        public QueueWorker<NetworkClientMessage> sendingWorker;

        public void Start()
        {
            sendingWorker?.Start();
            receivingWorker?.Start();
            connectionWorker?.Start();
        }
        public void Stop()
        {
            sendingWorker?.Stop();
            receivingWorker?.Stop();
            connectionWorker?.Join();
        }
    }
}
