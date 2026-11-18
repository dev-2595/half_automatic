using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace HalfAutomatic.Core
{
    /// <summary>
    /// 텍스트 한 덩어리를 체크섬과 함께 파일에 저장한다. 무결성만 담당하고 내용 형식은 모른다.
    ///
    /// 파일 형식: 1행 "GODQ-FILE-1", 2행 sha256(payload) 16진수, 3행부터 payload.
    /// 쓰기: 임시 파일에 먼저 쓰고 교체한다. 교체 직전 파일은 .bak으로 남긴다.
    /// 읽기: 본 파일이 없거나 손상되었으면 .bak을 시도한다(<see cref="LastReadUsedBackup"/>).
    /// </summary>
    public sealed class ChecksummedFileStore
    {
        private const string Magic = "GODQ-FILE-1";

        private readonly string _path;

        public string Path => _path;
        public string BackupPath => _path + ".bak";
        private string TempPath => _path + ".tmp";

        /// <summary>직전 Read가 백업 파일에서 복구했으면 true.</summary>
        public bool LastReadUsedBackup { get; private set; }

        public ChecksummedFileStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("path is required", nameof(path));
            }
            _path = path;
        }

        /// <summary>
        /// 본 파일 → 백업 순으로 읽는다. 둘 다 없으면 NotFound, 있는데 모두 손상이면 DataCorrupt,
        /// I/O 오류면 StorageFailure.
        /// </summary>
        public Result<string> Read()
        {
            LastReadUsedBackup = false;
            try
            {
                Result<string> main = ReadOne(_path);
                if (main.IsOk)
                {
                    return main;
                }
                Result<string> backup = ReadOne(BackupPath);
                if (backup.IsOk)
                {
                    LastReadUsedBackup = true;
                    return backup;
                }
                bool anyExists = main.Error != ErrorCode.NotFound || backup.Error != ErrorCode.NotFound;
                return Result<string>.Fail(anyExists ? ErrorCode.DataCorrupt : ErrorCode.NotFound);
            }
            catch (Exception e) when (IsIoFailure(e))
            {
                return Result<string>.Fail(ErrorCode.StorageFailure);
            }
        }

        public Result Write(string payload)
        {
            if (payload == null)
            {
                return Result.Fail(ErrorCode.InvalidArgument);
            }
            try
            {
                string dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(_path));
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                string content = Magic + "\n" + Sha256Hex(payload) + "\n" + payload;
                File.WriteAllText(TempPath, content, new UTF8Encoding(false));
                ReplaceWithTemp();
                return Result.Ok();
            }
            catch (Exception e) when (IsIoFailure(e))
            {
                TryDelete(TempPath);
                return Result.Fail(ErrorCode.StorageFailure);
            }
        }

        /// <summary>손상된 본 파일과 백업을 .corrupt로 옮겨 다음 저장이 덮어쓰지 않게 한다.</summary>
        public Result Quarantine()
        {
            try
            {
                MoveAside(_path, _path + ".corrupt");
                MoveAside(BackupPath, BackupPath + ".corrupt");
                return Result.Ok();
            }
            catch (Exception e) when (IsIoFailure(e))
            {
                return Result.Fail(ErrorCode.StorageFailure);
            }
        }

        private static Result<string> ReadOne(string path)
        {
            if (!File.Exists(path))
            {
                return Result<string>.Fail(ErrorCode.NotFound);
            }
            string text = File.ReadAllText(path, Encoding.UTF8);
            int first = text.IndexOf('\n');
            int second = first < 0 ? -1 : text.IndexOf('\n', first + 1);
            if (first < 0 || second < 0 || text.Substring(0, first) != Magic)
            {
                return Result<string>.Fail(ErrorCode.DataCorrupt);
            }
            string checksum = text.Substring(first + 1, second - first - 1);
            string payload = text.Substring(second + 1);
            return Sha256Hex(payload) == checksum
                ? Result<string>.Ok(payload)
                : Result<string>.Fail(ErrorCode.DataCorrupt);
        }

        private void ReplaceWithTemp()
        {
            if (!File.Exists(_path))
            {
                File.Move(TempPath, _path);
                return;
            }
            try
            {
                // 원자적 교체 + 직전 본을 백업으로 보존.
                File.Replace(TempPath, _path, BackupPath);
            }
            catch (PlatformNotSupportedException)
            {
                // File.Replace를 지원하지 않는 플랫폼: 백업으로 옮긴 뒤 이동(짧은 비원자 구간이 있지만 백업이 남는다).
                MoveAside(_path, BackupPath);
                File.Move(TempPath, _path);
            }
        }

        private static void MoveAside(string from, string to)
        {
            if (!File.Exists(from))
            {
                return;
            }
            if (File.Exists(to))
            {
                File.Delete(to);
            }
            File.Move(from, to);
        }

        private static string Sha256Hex(string text)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }

        private static bool IsIoFailure(Exception e)
        {
            return e is IOException || e is UnauthorizedAccessException || e is NotSupportedException
                || e is System.Security.SecurityException;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception e) when (IsIoFailure(e))
            {
                // 임시 파일 정리 실패는 무시한다. 다음 저장이 덮어쓴다.
            }
        }
    }
}
