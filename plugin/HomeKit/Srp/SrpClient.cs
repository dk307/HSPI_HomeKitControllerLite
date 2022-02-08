using NSec.Cryptography;
using System.Diagnostics;
using System.IO;
using System.Text;

#nullable enable


namespace HomeKit.Srp
{
    // Adapted from https://github.com/Jc2k/aiohomekit/blob/5902621ca0a1c8bd4399fcf8f2b7767d880cc252/aiohomekit/crypto/srp.py
    internal sealed class SrpClient
    {
        public SrpClient(string username,
                          string password,
                          byte[] salt,
                          byte[] serverPublicKey)
        {
            Debug.Assert(n.ToByteArray().Length == 3072 / 8);
            Debug.Assert(g.ToByteArray().Length == 1);
            Debug.Assert(salt.Length == 16);

            this.username = username;
            this.password = password;

            this.salt = SrpInteger.FromByteArray(salt);
            this.a = SrpInteger.RandomInteger(16);
            this.B = SrpInteger.FromByteArray(serverPublicKey);

            this.A = g.ModPow(a, n);
            this.publicKey = g.ModPow(a, n);
        }

        public SrpInteger GetProof()
        {
            var hN = ComputeHash(n).ToByteArray();
            var hg = ComputeHash(g).ToByteArray();
            for (var index = 0; index < hN.Length; index++)
            {
                hN[index] ^= hg[index];
            }

            var hu = ComputeHash(username);
            var K = GetSessionKey();

            return ComputeHash(SrpInteger.FromByteArray(hN), hu, salt, A, B, K);
        }

        public SrpInteger GetPublicKey() => publicKey;

        public SrpInteger GetSessionKey()
        {
            var secret = GetSharedSecret();
            return ComputeHash(secret);
        }

        public bool VerifyServersProof(SrpInteger proof)
        {
            return proof == ComputeHash(A, GetProof(), GetSessionKey());
        }

        private static SrpInteger CalculateK()
        {
            var gArray = new byte[384];
            gArray[383] = 5;
            return ComputeHash(n, SrpInteger.FromByteArray(gArray));
        }

        private static SrpInteger ComputeHash(params SrpInteger[] values)
        {
            using var stream = new MemoryStream();
            foreach (var value in values)
            {
                var data = value.ToByteArray();
                stream.Write(data, 0, data.Length);
            }
            var hashProvider = HashAlgorithm.Sha512;
            stream.Position = 0;
            return SrpInteger.FromByteArray(hashProvider.Hash(stream.ToArray()));
        }

        private static SrpInteger ComputeHash(SrpInteger value)
        {
            var hashProvider = HashAlgorithm.Sha512;
            return SrpInteger.FromByteArray(hashProvider.Hash(value.ToByteArray()));
        }

        private static SrpInteger ComputeHash(string value)
        {
            var hashProvider = HashAlgorithm.Sha512;
            var buffer = Encoding.UTF8.GetBytes(value);
            return SrpInteger.FromByteArray(hashProvider.Hash(buffer));
        }

        private SrpInteger CalculateU()
        {
            return ComputeHash(A, B);
        }

        private SrpInteger CalculateX()
        {
            string i = username + ":" + password;
            return ComputeHash(salt, ComputeHash(i));
        }

        private SrpInteger GetSharedSecret()
        {
            var u = CalculateU();
            var x = CalculateX();
            var k = CalculateK();
            var tmp1 = B - (k * g.ModPow(x, n));
            var tmp2 = a + (u * x);
            var S = tmp1.ModPow(tmp2, n);
            return S;
        }

        private static readonly SrpInteger g = SrpInteger.FromHex("05");
        private static readonly SrpInteger n = SrpInteger.FromHex("FFFFFFFFFFFFFFFFC90FDAA22168C234C4C6628B80DC1CD129024E088A67CC74020BBEA63B139B22514A08798E3404DDEF9519B3CD3A431B302B0A6DF25F14374FE1356D6D51C245E485B576625E7EC6F44C42E9A637ED6B0BFF5CB6F406B7EDEE386BFB5A899FA5AE9F24117C4B1FE649286651ECE45B3DC2007CB8A163BF0598DA48361C55D39A69163FA8FD24CF5F83655D23DCA3AD961C62F356208552BB9ED529077096966D670C354E4ABC9804F1746C08CA18217C32905E462E36CE3BE39E772C180E86039B2783A2EC07A28FB5C55DF06F4C52C9DE2BCBF6955817183995497CEA956AE515D2261898FA051015728E5A8AAAC42DAD33170D04507A33A85521ABDF1CBA64ECFB850458DBEF0A8AEA71575D060C7DB3970F85A6E1E4C7ABF5AE8CDB0933D71E8C94E04A25619DCEE3D2261AD2EE6BF12FFA06D98A0864D87602733EC86A64521F2B18177B200CBBE117577A615D6C770988C0BAD946E208E24FA074E5AB3143DB5BFCE0FD108E4B82D120A93AD2CAFFFFFFFFFFFFFFFF");
        private readonly SrpInteger a;
        private readonly SrpInteger A;
        private readonly SrpInteger publicKey;
        private readonly SrpInteger B;
        private readonly string password;
        private readonly SrpInteger salt;
        private readonly string username;
    }
}