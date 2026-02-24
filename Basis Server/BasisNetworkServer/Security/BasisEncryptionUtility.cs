using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace BasisNetworkServer.Security
{
    public static class BasisEncryptionUtility
    {
        // In a production environment, this key should be stored securely (e.g., Environment Variable, Key Vault)
        // For the purpose of the PSP project, we use a fixed key but document the need for secure storage.
        private static readonly byte[] MasterKey = Encoding.UTF8.GetBytes("BasisVRSecurityKey123!@#45678901"); // 32 bytes for AES-256

        public static string Encrypt(string plainText)
        {
            using Aes aes = Aes.Create();
            aes.Key = MasterKey;
            aes.GenerateIV();
            byte[] iv = aes.IV;

            using var encryptor = aes.CreateEncryptor(aes.Key, iv);
            using var ms = new MemoryStream();
            ms.Write(iv, 0, iv.Length); // Prepend IV to the stream

            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs))
            {
                sw.Write(plainText);
            }

            return Convert.ToBase64String(ms.ToArray());
        }

        public static string Decrypt(string cipherText)
        {
            byte[] fullCipher = Convert.FromBase64String(cipherText);

            using Aes aes = Aes.Create();
            aes.Key = MasterKey;

            byte[] iv = new byte[aes.BlockSize / 8];
            Array.Copy(fullCipher, 0, iv, 0, iv.Length);

            using var decryptor = aes.CreateDecryptor(aes.Key, iv);
            using var ms = new MemoryStream(fullCipher, iv.Length, fullCipher.Length - iv.Length);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);

            return sr.ReadToEnd();
        }
    }
}
