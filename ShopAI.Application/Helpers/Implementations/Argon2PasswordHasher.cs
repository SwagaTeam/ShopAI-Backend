using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using ShopAI.Application.Helpers.Abstractions;

namespace ShopAI.Infrastructure.Security;

public class Argon2PasswordHasher : IPasswordHasher
{
    private const int DegreeOfParallelism = 8; // Количество потоков
    private const int MemorySize = 65536;      // Использование RAM (64 MB)
    private const int Iterations = 4;          // Количество итераций
    private const int HashLength = 32;         // Длина хеша (в байтах)
    private const int SaltLength = 16;         // Длина соли (в байтах)

    public (string Hash, string Salt) Hash(string password)
    {
        var saltBytes = new byte[SaltLength];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(saltBytes);
        }

        var hashBytes = HashPassword(password, saltBytes);

        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
    }

    public bool Verify(string password, string hash, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var expectedHashBytes = Convert.FromBase64String(hash);

        var actualHashBytes = HashPassword(password, saltBytes);

        return CryptographicOperations.FixedTimeEquals(actualHashBytes, expectedHashBytes);
    }

    private byte[] HashPassword(string password, byte[] salt)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(password);

        using var argon2 = new Argon2id(passwordBytes)
        {
            Salt = salt,
            DegreeOfParallelism = DegreeOfParallelism,
            MemorySize = MemorySize,
            Iterations = Iterations
        };

        return argon2.GetBytes(HashLength);
    }
}