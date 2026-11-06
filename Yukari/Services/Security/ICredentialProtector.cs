namespace Yukari.Services.Security;

internal interface ICredentialProtector
{
    byte[] Encrypt(string plainText);
    string Decrypt(byte[] cipherText);
}
