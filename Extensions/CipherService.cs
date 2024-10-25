using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace VizInvoiceGeneratorWebAPI.Services.Extensions
{
    public static class CipherService
    {
        public static string Encrypt(string plainText, string cipherKey, string cipherIV)
        {
            using (Aes encryptor = Aes.Create())
            {
                encryptor.BlockSize = 128;
                encryptor.KeySize = 256;
                encryptor.IV = Encoding.UTF8.GetBytes(cipherIV);
                encryptor.Key = Encoding.UTF8.GetBytes(cipherKey);
                encryptor.Mode = CipherMode.CBC;
                encryptor.Padding = PaddingMode.PKCS7;
                byte[] data = Encoding.UTF8.GetBytes(plainText);
                using (ICryptoTransform encrypt = encryptor.CreateEncryptor())
                {
                    byte[] dest = encrypt.TransformFinalBlock(data, 0, data.Length);
                    return Convert.ToBase64String(dest);
                }
            }
        }

        public static string Decrypt(string encryptedText, string cipherKey, string cipherIV)
        {
            string? plaintext = null;
            using (Aes encryptor = Aes.Create())
            {
                byte[] cipherText = Convert.FromBase64String(encryptedText);
                byte[] aesIV = Encoding.UTF8.GetBytes(cipherIV);
                byte[] aesKey = Encoding.UTF8.GetBytes(cipherKey);

                ICryptoTransform decryptor = encryptor.CreateDecryptor(aesKey, aesIV);
                using (MemoryStream ms = new MemoryStream(cipherText))
                {
                    using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader reader = new StreamReader(cs))
                            plaintext = reader.ReadToEnd();
                    }
                }
            }
            return plaintext;
        }
    }
}
