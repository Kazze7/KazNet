namespace KazNet.SslCertificate
{
    public struct SslCertificateConfig
    {
        public SslCertificateKeySize keySize;
        public string distinguishedName;
        public string password;
        public string filePathPfx;
        public string filePathCer;
    }
}
