using KazDev.Core;

namespace KazNet.Core
{
    class NetworkThread
    {
        public int connectionCount = 0;
        public Thread connectionWorker;
        public AutoResetEvent nextClientEvent = new AutoResetEvent(false);
        public AutoResetEvent nextSendEvent = new AutoResetEvent(false);
        public QueueWorker<Packet> receivingWorker;
        public QueueWorker<Packet> sendingWorker;
    }
}
