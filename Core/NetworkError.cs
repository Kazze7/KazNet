namespace KazNet.Core
{
    public enum NetworkError
    {
        errorListener,
        errorConnection,
        errorReadStream,
        errorWriteStream,
        invalidServerCertificate,
        remoteCertificateChainErrors
    }
}
