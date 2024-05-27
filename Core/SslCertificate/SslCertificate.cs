using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace KazNet.Ssl
{
    public static class SslCertificate
    {
        public static void New(SslCertificateKeySize _keySize, string _distinguishedName, string _password, string _filePathPfx, string _filePathCer)
        {
            RSA rsa = RSA.Create((int)_keySize);
            CertificateRequest certRequest = new CertificateRequest(_distinguishedName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            X509Certificate2 cert = certRequest.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(1));
            // Create PFX (PKCS #12) with private key
            File.WriteAllBytes(_filePathPfx, cert.Export(X509ContentType.Pfx, _password));
            // Create Base 64 encoded CER (public key only)
            File.WriteAllText(_filePathCer,
                "-----BEGIN CERTIFICATE-----\r\n"
                + Convert.ToBase64String(cert.Export(X509ContentType.Cert), Base64FormattingOptions.InsertLineBreaks)
                + "\r\n-----END CERTIFICATE-----");
        }
        public static void New(SslCertificateConfig _sslCertConfig) { New(_sslCertConfig.keySize, _sslCertConfig.distinguishedName, _sslCertConfig.password, _sslCertConfig.filePathPfx, _sslCertConfig.filePathCer); }
    }
}
