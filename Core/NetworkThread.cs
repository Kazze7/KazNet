using KazDev.Core;

namespace KazNet.Core
{
    class NetworkThread
    {
        public int connectionCount = 0;
        public Thread connectionWorker;
        public QueueWorker<NetworkPacket> receivingWorker;
        public QueueWorker<NetworkPacket> sendingWorker;
        public AutoResetEvent nextConnectionEvent = new AutoResetEvent(false);

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
            nextConnectionEvent.Set();
            connectionWorker?.Join();
        }
    }
}
