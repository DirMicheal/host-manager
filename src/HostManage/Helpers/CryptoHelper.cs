using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace HostManage.Helpers;

public static class CryptoHelper
{
    private static readonly Encoding DefaultEncoding = Encoding.UTF8;

    public static string DPAPI_Encrypt(string plainText)
    {
        try
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            byte[] data = DefaultEncoding.GetBytes(plainText);
            byte[] encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }
        catch (CryptographicException)
        {
            return string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    public static string DPAPI_Decrypt(string cipherText)
    {
        try
        {
            if (string.IsNullOrEmpty(cipherText))
                return string.Empty;

            byte[] data = Convert.FromBase64String(cipherText);
            byte[] decrypted = ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
            return DefaultEncoding.GetString(decrypted);
        }
        catch (CryptographicException)
        {
            return string.Empty;
        }
        catch (FormatException)
        {
            return string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    public static string AES_Encrypt(string plainText, byte[] key, byte[] iv)
    {
        try
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;
            if (key == null || key.Length != 32)
                throw new ArgumentException("AES密钥必须为32字节(256位)", nameof(key));
            if (iv == null || iv.Length != 16)
                throw new ArgumentException("AES IV必须为16字节(128位)", nameof(iv));

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs, DefaultEncoding))
            {
                sw.Write(plainText);
            }

            byte[] encrypted = ms.ToArray();
            return Convert.ToBase64String(encrypted);
        }
        catch (CryptographicException)
        {
            return string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    public static string AES_Decrypt(string cipherText, byte[] key, byte[] iv)
    {
        try
        {
            if (string.IsNullOrEmpty(cipherText))
                return string.Empty;
            if (key == null || key.Length != 32)
                throw new ArgumentException("AES密钥必须为32字节(256位)", nameof(key));
            if (iv == null || iv.Length != 16)
                throw new ArgumentException("AES IV必须为16字节(128位)", nameof(iv));

            byte[] cipherBytes = Convert.FromBase64String(cipherText);

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            using var ms = new MemoryStream(cipherBytes);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs, DefaultEncoding);

            return sr.ReadToEnd();
        }
        catch (CryptographicException)
        {
            return string.Empty;
        }
        catch (FormatException)
        {
            return string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    public static byte[] GenerateKey()
    {
        try
        {
            using var aes = Aes.Create();
            aes.GenerateKey();
            return aes.Key;
        }
        catch (Exception)
        {
            return new byte[32];
        }
    }

    public static string SHA256_Hash(string content)
    {
        try
        {
            if (string.IsNullOrEmpty(content))
                return string.Empty;

            using var sha256 = SHA256.Create();
            byte[] bytes = DefaultEncoding.GetBytes(content);
            byte[] hashBytes = sha256.ComputeHash(bytes);

            var sb = new StringBuilder();
            for (int i = 0; i < hashBytes.Length; i++)
            {
                sb.Append(hashBytes[i].ToString("x2"));
            }

            return sb.ToString();
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
