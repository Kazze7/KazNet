using KazDev.Core;

namespace KazNet.Core
{
    class ClientThread
    {
        public Thread connectionThread;
        public AutoResetEvent nextSendEvent = new AutoResetEvent(false);
        public QueueWorker<Packet> receivingWorker;
        public QueueWorker<Packet> sendingWorker;
    }
}
