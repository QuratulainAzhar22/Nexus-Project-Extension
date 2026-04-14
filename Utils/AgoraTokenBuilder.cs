using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Nexus_backend.Utils
{
    public static class AgoraTokenBuilder
    {
        private const string Version = "006";
        public const int RolePublisher = 1;
        public const int RoleSubscriber = 2;

        public static string BuildToken(
            string appId,
            string appCertificate,
            string channelName,
            uint uid,
            int role,
            int expireSeconds = 3600)
        {
            uint ts = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            uint salt = (uint)new Random().Next(1, int.MaxValue);
            uint expiredTs = ts + (uint)expireSeconds;

            var privileges = new SortedDictionary<ushort, uint>
            {
                { 1, expiredTs }, // JoinChannel
                { 2, expiredTs }, // PublishAudioStream
                { 3, expiredTs }, // PublishVideoStream
            };

            string uidStr = uid == 0 ? "" : uid.ToString();
            byte[] msg = PackMessage(salt, ts, privileges);
            byte[] sig = GenerateSignature(appCertificate, appId, channelName, uidStr, msg);
            byte[] content = PackContent(sig, msg);
            byte[] compressed = ZlibCompress(content);

            return Version + appId + Convert.ToBase64String(compressed);
        }

        private static byte[] PackMessage(uint salt, uint ts, SortedDictionary<ushort, uint> privileges)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            bw.Write(salt);
            bw.Write(ts);
            bw.Write((ushort)privileges.Count);
            foreach (var kv in privileges)
            {
                bw.Write(kv.Key);
                bw.Write(kv.Value);
            }
            return ms.ToArray();
        }

        private static byte[] GenerateSignature(string appCertificate, string appId, string channelName, string uid, byte[] msg)
        {
            using var ms = new MemoryStream();
            byte[] a = Encoding.UTF8.GetBytes(appId);
            byte[] c = Encoding.UTF8.GetBytes(channelName);
            byte[] u = Encoding.UTF8.GetBytes(uid);
            ms.Write(a, 0, a.Length);
            ms.Write(c, 0, c.Length);
            ms.Write(u, 0, u.Length);
            ms.Write(msg, 0, msg.Length);

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appCertificate));
            return hmac.ComputeHash(ms.ToArray());
        }

        private static byte[] PackContent(byte[] sig, byte[] msg)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            bw.Write((ushort)sig.Length);
            bw.Write(sig);
            bw.Write(msg, 0, msg.Length);
            return ms.ToArray();
        }

        private static byte[] ZlibCompress(byte[] data)
        {
            using var output = new MemoryStream();
            output.WriteByte(0x78);
            output.WriteByte(0x9C);
            using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
            {
                deflate.Write(data, 0, data.Length);
            }
            uint adler = Adler32(data);
            output.WriteByte((byte)(adler >> 24));
            output.WriteByte((byte)(adler >> 16));
            output.WriteByte((byte)(adler >> 8));
            output.WriteByte((byte)adler);
            return output.ToArray();
        }

        private static uint Adler32(byte[] data)
        {
            const uint MOD = 65521;
            uint a = 1, b = 0;
            foreach (byte bt in data)
            {
                a = (a + bt) % MOD;
                b = (b + a) % MOD;
            }
            return (b << 16) | a;
        }
    }
}