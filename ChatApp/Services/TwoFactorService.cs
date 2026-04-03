using System.Security.Cryptography;
using System.Text;

namespace ChatApp.Services
{
    public static class TwoFactorService
    {
        private const int SecretLength = 20; // 160-bit
        private const int CodeDigits = 6;
        private const int TimeStepSeconds = 30;

        public static string GenerateSecret()
        {
            var bytes = new byte[SecretLength];
            RandomNumberGenerator.Fill(bytes);
            return Base32Encode(bytes);
        }

        public static string GetProvisioningUri(string issuer, string accountName, string secret)
        {
            var encodedIssuer = Uri.EscapeDataString(issuer);
            var encodedAccount = Uri.EscapeDataString(accountName);
            return $"otpauth://totp/{encodedIssuer}:{encodedAccount}?secret={secret}&issuer={encodedIssuer}&digits={CodeDigits}&period={TimeStepSeconds}";
        }

        public static bool ValidateTotpCode(string secret, string code)
        {
            if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code)) return false;

            var key = Base32Decode(secret);
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / TimeStepSeconds;

            for (long counter = timestamp - 1; counter <= timestamp + 1; counter++)
            {
                var expected = GenerateTotp(key, counter);
                if (expected == code) return true;
            }

            return false;
        }

        private static string GenerateTotp(byte[] key, long counter)
        {
            var counterBytes = BitConverter.GetBytes(counter);
            if (BitConverter.IsLittleEndian) Array.Reverse(counterBytes);

            using var hmac = new HMACSHA1(key);
            var hash = hmac.ComputeHash(counterBytes);
            var offset = hash[^1] & 0x0F;

            var binaryCode = ((hash[offset] & 0x7F) << 24)
                           | ((hash[offset + 1] & 0xFF) << 16)
                           | ((hash[offset + 2] & 0xFF) << 8)
                           | (hash[offset + 3] & 0xFF);

            var otp = binaryCode % (int)Math.Pow(10, CodeDigits);
            return otp.ToString($"D{CodeDigits}");
        }

        private static string Base32Encode(byte[] data)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            var output = new StringBuilder();
            int buffer = data[0];
            int next = 1;
            int bitsLeft = 8;

            while (bitsLeft > 0 || next < data.Length)
            {
                if (bitsLeft < 5)
                {
                    if (next < data.Length)
                    {
                        buffer <<= 8;
                        buffer |= data[next++] & 0xff;
                        bitsLeft += 8;
                    }
                    else
                    {
                        int pad = 5 - bitsLeft;
                        buffer <<= pad;
                        bitsLeft += pad;
                    }
                }

                int index = (buffer >> (bitsLeft - 5)) & 0x1F;
                bitsLeft -= 5;
                output.Append(alphabet[index]);
            }

            return output.ToString();
        }

        private static byte[] Base32Decode(string base32)
        {
            base32 = base32.TrimEnd('=');
            var bytes = new List<byte>();
            int buffer = 0;
            int bitsLeft = 0;

            foreach (char c in base32)
            {
                int val = c switch
                {
                    >= 'A' and <= 'Z' => c - 'A',
                    >= '2' and <= '7' => c - '2' + 26,
                    >= 'a' and <= 'z' => c - 'a',
                    _ => throw new FormatException("Invalid Base32 character."),
                };

                buffer = (buffer << 5) | val;
                bitsLeft += 5;

                if (bitsLeft >= 8)
                {
                    bytes.Add((byte)((buffer >> (bitsLeft - 8)) & 0xFF));
                    bitsLeft -= 8;
                }
            }

            return bytes.ToArray();
        }
    }
}
