using KazDev.Core;

namespace KazNet.Core
{
    class ConnectionServerThreads
    {
        public int connectionCount = 0;
        public Thread connectionWorker;
        public AutoResetEvent connectionEvent = new AutoResetEvent(false);
        public QueueWorker<NetworkServerMessage> receivingWorker;
        public QueueWorker<NetworkServerMessage> sendingWorker;

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
            connectionEvent.Set();
            connectionWorker?.Join();
        }
    }
}
