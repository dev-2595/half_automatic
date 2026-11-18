using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using HalfAutomatic.Core;

namespace HalfAutomatic.Tests
{
    /// <summary>미리 정한 값을 순서대로 돌려주는 난수. 값이 모자라면 예외로 테스트 실수를 드러낸다.</summary>
    internal sealed class ScriptedRandom : IRandom
    {
        private readonly Queue<double> _doubles;
        private readonly Queue<int> _ints;

        public ScriptedRandom(IEnumerable<double> doubles, IEnumerable<int> ints)
        {
            _doubles = new Queue<double>(doubles);
            _ints = new Queue<int>(ints);
        }

        public double NextDouble()
        {
            if (_doubles.Count == 0) throw new InvalidOperationException("no scripted double left");
            return _doubles.Dequeue();
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            if (_ints.Count == 0) throw new InvalidOperationException("no scripted int left");
            int value = _ints.Dequeue();
            if (value < minInclusive || value >= maxExclusive)
            {
                throw new InvalidOperationException($"scripted int {value} outside [{minInclusive},{maxExclusive})");
            }
            return value;
        }
    }

    internal sealed class FakeClock : IClock
    {
        public long UnixSeconds { get; set; } = 1_000;
    }

    /// <summary>
    /// 테스트용 직렬화기. Unity(Mono)와 .NET 양쪽에서 기본 제공되는 XmlSerializer를 쓴다.
    /// (실제 게임은 Unity JsonUtility 구현을 주입한다.)
    /// </summary>
    internal sealed class XmlTestSerializer<T> : ISerializer<T> where T : class
    {
        private readonly XmlSerializer _xml = new XmlSerializer(typeof(T));

        public string Serialize(T value)
        {
            using (var writer = new StringWriter())
            {
                _xml.Serialize(writer, value);
                return writer.ToString();
            }
        }

        public Result<T> Deserialize(string text)
        {
            try
            {
                using (var reader = new StringReader(text ?? string.Empty))
                {
                    var value = _xml.Deserialize(reader) as T;
                    return value == null ? Result<T>.Fail(ErrorCode.DataCorrupt) : Result<T>.Ok(value);
                }
            }
            catch (InvalidOperationException)
            {
                return Result<T>.Fail(ErrorCode.DataCorrupt);
            }
            catch (XmlException)
            {
                return Result<T>.Fail(ErrorCode.DataCorrupt);
            }
        }
    }

    /// <summary>메모리 계정 저장소. FailNext로 저장소 오류를 흉내 낸다.</summary>
    internal sealed class InMemoryAccountStore : IAccountStore
    {
        private readonly Dictionary<string, AccountRecord> _records = new Dictionary<string, AccountRecord>();
        public ErrorCode FindError = ErrorCode.None;
        public ErrorCode SaveError = ErrorCode.None;
        public int SaveCount;

        public Result<AccountRecord> Find(string key)
        {
            if (FindError != ErrorCode.None) return Result<AccountRecord>.Fail(FindError);
            return _records.TryGetValue(key, out AccountRecord r)
                ? Result<AccountRecord>.Ok(r)
                : Result<AccountRecord>.Fail(ErrorCode.NotFound);
        }

        public Result Save(AccountRecord record)
        {
            if (SaveError != ErrorCode.None) return Result.Fail(SaveError);
            _records[record.Key] = record;
            SaveCount++;
            return Result.Ok();
        }
    }

    /// <summary>테스트마다 새 임시 폴더를 만들고 정리한다.</summary>
    internal sealed class TempDir : IDisposable
    {
        public string Path { get; } =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "godq_tests_" + Guid.NewGuid().ToString("N"));

        public TempDir()
        {
            Directory.CreateDirectory(Path);
        }

        public string File(string name) => System.IO.Path.Combine(Path, name);

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, true);
            }
            catch (IOException)
            {
            }
        }
    }

    internal static class Fixtures
    {
        public static BalanceConfig Config() => BalanceConfig.CreateDefault();

        /// <summary>테스트 속도를 위해 반복 횟수를 최소로 한 해시기.</summary>
        public static PasswordHasher FastHasher() => new PasswordHasher(PasswordHasher.MinIterations);

        public static Wallet Rich()
        {
            var wallet = new Wallet();
            wallet.AddCoins(1_000_000_000);
            wallet.AddScrolls(WeaponCategory.Melee, 1_000_000);
            wallet.AddScrolls(WeaponCategory.Magic, 1_000_000);
            wallet.AddScrolls(WeaponCategory.Heal, 1_000_000);
            return wallet;
        }
    }
}
