using System.Security.Cryptography;

namespace SASMS.Web.Security;

public static class TemporaryPasswordGenerator
{
    /// <summary>High-entropy random password (mixed case, digits, symbols) for first-login use only.</summary>
    public static string Generate() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));
}
