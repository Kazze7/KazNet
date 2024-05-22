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
        errorReadPacket,
        errorSendPacket,
        connectionsLimit,
        invalidServerCertificate,
        remoteCertificateChainErrors
    }
}
