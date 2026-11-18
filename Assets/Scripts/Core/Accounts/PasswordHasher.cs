using System;
using System.Security.Cryptography;
using System.Text;

namespace HalfAutomatic.Core
{
    /// <summary>저장된 비밀번호 해시(불변). 비밀번호 원문은 어디에도 저장하지 않는다.</summary>
    public sealed class PasswordHash
    {
        private readonly byte[] _salt;
        private readonly byte[] _hash;

        public int Iterations { get; }
        public byte[] Salt => (byte[])_salt.Clone();
        public byte[] Hash => (byte[])_hash.Clone();

        public PasswordHash(byte[] salt, byte[] hash, int iterations)
        {
            if (salt == null || salt.Length < PasswordHasher.MinSaltBytes)
            {
                throw new ArgumentException("salt is too short", nameof(salt));
            }
            if (hash == null || hash.Length != PasswordHasher.HashBytes)
            {
                throw new ArgumentException("hash has wrong length", nameof(hash));
            }
            if (iterations < PasswordHasher.MinIterations)
            {
                throw new ArgumentOutOfRangeException(nameof(iterations));
            }
            _salt = (byte[])salt.Clone();
            _hash = (byte[])hash.Clone();
            Iterations = iterations;
        }
    }

    /// <summary>
    /// PBKDF2-HMAC-SHA256 비밀번호 해시.
    /// 반복 횟수 기본값 600,000은 OWASP Password Storage Cheat Sheet의 PBKDF2-HMAC-SHA256 권장값을 따른다.
    /// 반복 횟수는 해시와 함께 저장하므로 나중에 올려도 기존 계정이 그대로 검증된다.
    /// </summary>
    public sealed class PasswordHasher
    {
        public const int DefaultIterations = 600_000;
        public const int MinIterations = 1_000;
        public const int SaltBytes = 16;
        public const int MinSaltBytes = 16;
        public const int HashBytes = 32;

        public int Iterations { get; }

        public PasswordHasher(int iterations = DefaultIterations)
        {
            if (iterations < MinIterations)
            {
                throw new ArgumentOutOfRangeException(nameof(iterations));
            }
            Iterations = iterations;
        }

        public PasswordHash Hash(string password)
        {
            if (password == null)
            {
                throw new ArgumentNullException(nameof(password));
            }
            byte[] salt = new byte[SaltBytes];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            return new PasswordHash(salt, Derive(password, salt, Iterations), Iterations);
        }

        public bool Verify(string password, PasswordHash stored)
        {
            if (password == null || stored == null)
            {
                return false;
            }
            byte[] actual = Derive(password, stored.Salt, stored.Iterations);
            return FixedTimeEquals(actual, stored.Hash);
        }

        private static byte[] Derive(string password, byte[] salt, int iterations)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(Encoding.UTF8.GetBytes(password), salt, iterations,
                       HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(HashBytes);
            }
        }

        /// <summary>길이가 같으면 내용과 무관하게 같은 시간이 걸리는 비교(타이밍 공격 방지).</summary>
        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }
            return diff == 0;
        }
    }
}
