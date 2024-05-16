namespace KazNet.Core
{
    public enum NetworkStatus
    {
        stopped,
        started,
        launched,
        connected,
        errorListener,
        errorConnection,
        errorRecivePacket,
        errorSendPacket,
        clientsLimit
    }
}
