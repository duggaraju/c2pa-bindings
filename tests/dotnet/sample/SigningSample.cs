// This file will demo how to use the C# C2PA SDK to sign media using an X509 Certificate.

using C2pa;

class X509Signer : ISignerCallback
{
    public int Sign(ReadOnlySpan<byte> data, Span<byte> hash)
    {
        // Your signing logic here
        return 0;
    }

    public SignerConfig Config => new SignerConfig
    {

    };
}