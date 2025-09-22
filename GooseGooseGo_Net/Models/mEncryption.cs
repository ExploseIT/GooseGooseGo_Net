

namespace GooseGooseGo_Net.Models
{
    using System.Security.Cryptography;
    using System.Text;

    public static class mEncryption
    {
        // Encrypts a string using AES and returns Base64 string (with IV prepended)
        public static string EncryptString(string plainText, string password)
        {
            using var aes = Aes.Create();
            var key = GetAesKey(password, aes.KeySize / 8);
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor(key, aes.IV);
            using var ms = new MemoryStream();
            ms.Write(aes.IV, 0, aes.IV.Length); // Prepend IV
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs))
            {
                sw.Write(plainText);
            }
            return Convert.ToBase64String(ms.ToArray());
        }

        // Decrypts a Base64 string (with IV prepended) using AES
        public static string DecryptString(string cipherText, string password)
        {
            var fullCipher = Convert.FromBase64String(cipherText);
            using var aes = Aes.Create();
            var key = GetAesKey(password, aes.KeySize / 8);

            var iv = new byte[aes.BlockSize / 8];
            Array.Copy(fullCipher, 0, iv, 0, iv.Length);

            using var decryptor = aes.CreateDecryptor(key, iv);
            using var ms = new MemoryStream(fullCipher, iv.Length, fullCipher.Length - iv.Length);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);
            return sr.ReadToEnd();
        }

        // Helper: Derive AES key from password
        private static byte[] GetAesKey(string password, int keyBytes)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return hash.Take(keyBytes).ToArray();
        }
    }
}
