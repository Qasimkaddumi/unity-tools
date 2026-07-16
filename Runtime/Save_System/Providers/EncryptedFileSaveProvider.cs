using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Kaddumi.UnityTools.Save.Providers
{
    /// <summary>
    /// <see cref="FileSaveProvider"/> variant that encrypts each save with AES-256 (CBC)
    /// using a key derived from a password via PBKDF2. A fresh random salt and IV are
    /// generated per write and stored in the file header, so identical saves produce
    /// different ciphertext and no key material is written to disk.
    ///
    /// This deters casual save-file tampering; it is not a defense against a determined
    /// attacker who can read the app binary (the password ships inside it). For that,
    /// derive the password from a server-issued or per-device secret.
    /// </summary>
    public class EncryptedFileSaveProvider : FileSaveProvider
    {
        private const int SaltSize = 16;   // bytes
        private const int IvSize = 16;     // AES block size
        private const int KeySize = 32;    // 256-bit key
        private const int Iterations = 100_000;

        // Rfc2898DeriveBytes' default (PBKDF2-HMAC-SHA1) is used so the crypto compiles on
        // both .NET Standard 2.0 and 2.1 consumer projects — the SHA256 overload is 2.1-only.

        private readonly byte[] passwordBytes;

        public EncryptedFileSaveProvider(string password, string subFolder = "Saves", string extension = ".sav")
            : base(subFolder, extension)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("Encryption password must not be empty.", nameof(password));
            }
            passwordBytes = Encoding.UTF8.GetBytes(password);
        }

        protected override byte[] Encode(string data)
        {
            byte[] salt = RandomBytes(SaltSize);
            byte[] iv = RandomBytes(IvSize);

            using (var kdf = new Rfc2898DeriveBytes(passwordBytes, salt, Iterations))
            using (var aes = Aes.Create())
            {
                aes.Key = kdf.GetBytes(KeySize);
                aes.IV = iv;

                byte[] plain = Encoding.UTF8.GetBytes(data);
                using (var encryptor = aes.CreateEncryptor())
                {
                    byte[] cipher = encryptor.TransformFinalBlock(plain, 0, plain.Length);

                    // Header layout: [salt][iv][cipher]
                    var output = new byte[SaltSize + IvSize + cipher.Length];
                    Buffer.BlockCopy(salt, 0, output, 0, SaltSize);
                    Buffer.BlockCopy(iv, 0, output, SaltSize, IvSize);
                    Buffer.BlockCopy(cipher, 0, output, SaltSize + IvSize, cipher.Length);
                    return output;
                }
            }
        }

        protected override string Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length < SaltSize + IvSize)
            {
                throw new InvalidDataException("Encrypted save is too short or truncated.");
            }

            var salt = new byte[SaltSize];
            var iv = new byte[IvSize];
            Buffer.BlockCopy(bytes, 0, salt, 0, SaltSize);
            Buffer.BlockCopy(bytes, SaltSize, iv, 0, IvSize);

            int cipherLength = bytes.Length - SaltSize - IvSize;
            var cipher = new byte[cipherLength];
            Buffer.BlockCopy(bytes, SaltSize + IvSize, cipher, 0, cipherLength);

            using (var kdf = new Rfc2898DeriveBytes(passwordBytes, salt, Iterations))
            using (var aes = Aes.Create())
            {
                aes.Key = kdf.GetBytes(KeySize);
                aes.IV = iv;
                using (var decryptor = aes.CreateDecryptor())
                {
                    // A wrong password / tampered file throws CryptographicException here,
                    // which FileSaveProvider.Read surfaces as SaveErrorType.Corrupted.
                    byte[] plain = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
                    return Encoding.UTF8.GetString(plain);
                }
            }
        }

        private static byte[] RandomBytes(int size)
        {
            var bytes = new byte[size];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return bytes;
        }
    }
}
