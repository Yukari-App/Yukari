namespace Yukari.Models.Data;

public record CredentialProtectionConfig(byte[] Salt, byte[] Canary, int Iterations);
