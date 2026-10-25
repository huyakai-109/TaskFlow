using System.Security.Cryptography;
using System.Text;

namespace TaskFlow.Common.Helpers
{
    public static class RsaHelper
    {
        /// <summary>
        /// Encrypt data using the public key
        /// </summary>
        /// <param name="data">Data in plain text</param>
        /// <param name="publicKey">Public key</param>
        /// <returns>Encrypted string</returns>
        public static string Encrypt(this string data, string publicKey)
        {
            var rsa = new RSACryptoServiceProvider();
            rsa.FromXmlString(publicKey.Base64Decode());

            var encoder = new UnicodeEncoding();

            var dataToEncrypt = encoder.GetBytes(data);
            var encryptedByteArray = rsa.Encrypt(dataToEncrypt, false).ToArray();
            var item = 0;
            var sb = new StringBuilder();
            foreach (var x in encryptedByteArray)
            {
                item++;
                sb.Append(x);

                if (item < encryptedByteArray.Length)
                {
                    sb.Append(',');
                }
            }

            return sb.ToString().Base64Encode();
        }

        /// <summary>
        /// Decrypt using the private key
        /// </summary>
        /// <param name="data">Data in base64</param>
        /// <param name="privateKey">Private Key</param>
        /// <returns>Decrypted string</returns>
        public static string Decrypt(this string data, string privateKey)
        {
            var decoded = data.Base64Decode();

            var rsa = new RSACryptoServiceProvider();
            var dataArray = decoded.Split([',']);
            var dataByte = new byte[dataArray.Length];
            for (var i = 0; i < dataArray.Length; i++)
            {
                dataByte[i] = Convert.ToByte(dataArray[i]);
            }

            rsa.FromXmlString(privateKey.Base64Decode());
            var decryptedByte = rsa.Decrypt(dataByte, false);
            var encoder = new UnicodeEncoding();

            return encoder.GetString(decryptedByte);
        }

        /// <summary>
        /// Encrypt using a single secret key
        /// </summary>
        /// <param name="data">Data in plain text</param>
        /// <param name="secretKey">Secret key</param>
        /// <returns>Encrypted string</returns>
        public static string EncryptSingleKey(this string data, string secretKey)
        {
            var aes = Aes.Create();
            var key = Encoding.UTF8.GetBytes(secretKey[..32]);
            aes.Key = key;
            aes.IV = new byte[16];

            using var memoryStream = new MemoryStream();
            using (var cryptoStream = new CryptoStream(memoryStream, aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                using var streamWriter = new StreamWriter(cryptoStream);
                streamWriter.Write(data);
            }
            return Convert.ToBase64String(memoryStream.ToArray());
        }

        /// <summary>
        /// Decrypt using a single secret key
        /// </summary>
        /// <param name="data">Data in base64</param>
        /// <param name="secretKey">Secret key</param>
        /// <returns>Decrypted string</returns>
        public static string DecryptSingleKey(this string data, string secretKey)
        {
            var aes = Aes.Create();
            var key = Encoding.UTF8.GetBytes(secretKey[..32]);
            aes.Key = key;
            aes.IV = new byte[16];

            using var memoryStream = new MemoryStream(Convert.FromBase64String(data));
            using var cryptoStream = new CryptoStream(memoryStream, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var streamReader = new StreamReader(cryptoStream);
            return streamReader.ReadToEnd();
        }
    }
}
