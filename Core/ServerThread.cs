using KazDev.Core;

namespace KazNet.Core
{
    class ServerThread
    {
        public int clientCount = 0;
        public Thread connectionThread;
        public AutoResetEvent nextClientEvent = new AutoResetEvent(false);
        public AutoResetEvent nextSendEvent = new AutoResetEvent(false);
        public QueueWorker<Packet> receivingWorker;
        public QueueWorker<Packet> sendingWorker;
    }
}
