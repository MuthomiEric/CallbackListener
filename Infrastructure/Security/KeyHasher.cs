using System.Security.Cryptography;
using System.Text;

namespace CallbackListener.Infrastructure.Security;

public static class KeyHasher
{
    public static string Hash(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
