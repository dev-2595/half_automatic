using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using HalfAutomatic.Core;
using UnityEngine;

namespace HalfAutomatic.Platform
{
    /// <summary>
    /// 프로필을 JSON 파일로 저장한다. 파일 I/O와 무결성 검사만 담당하고 규칙 검증은 PlayerProfile에 맡긴다.
    /// 형식: {version, checksum(sha256), payload(JSON 문자열)}.
    /// 쓰기는 임시 파일 → 교체 순서라 도중에 중단돼도 기존 파일이 남는다.
    /// </summary>
    public sealed class FileProfileStore : IProfileStore
    {
        private const int FormatVersion = 1;

        private readonly string _path;

        public FileProfileStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("path is required", nameof(path));
            }
            _path = path;
        }

        public static FileProfileStore CreateDefault()
        {
            return new FileProfileStore(Path.Combine(Application.persistentDataPath, "profile.json"));
        }

        public Result<PlayerProfile> Load()
        {
            try
            {
                if (!File.Exists(_path))
                {
                    return Result<PlayerProfile>.Fail(ErrorCode.NoProfile);
                }

                Envelope envelope = ParseOrNull<Envelope>(File.ReadAllText(_path, Encoding.UTF8));
                if (envelope == null
                    || envelope.version != FormatVersion
                    || string.IsNullOrEmpty(envelope.payload)
                    || Sha256Hex(envelope.payload) != envelope.checksum)
                {
                    return Result<PlayerProfile>.Fail(ErrorCode.DataCorrupt);
                }

                ProfileDto dto = ParseOrNull<ProfileDto>(envelope.payload);
                if (dto == null)
                {
                    return Result<PlayerProfile>.Fail(ErrorCode.DataCorrupt);
                }

                Result<PlayerProfile> profile = PlayerProfile.Create(dto.id, dto.nickname, dto.createdAtUnix);
                return profile.IsOk ? profile : Result<PlayerProfile>.Fail(ErrorCode.DataCorrupt);
            }
            catch (Exception e) when (IsIoFailure(e))
            {
                return Result<PlayerProfile>.Fail(ErrorCode.StorageFailure);
            }
        }

        public Result Save(PlayerProfile profile)
        {
            if (profile == null)
            {
                return Result.Fail(ErrorCode.InvalidProfileData);
            }

            string tmpPath = _path + ".tmp";
            try
            {
                string payload = JsonUtility.ToJson(new ProfileDto
                {
                    id = profile.Id,
                    nickname = profile.Nickname,
                    createdAtUnix = profile.CreatedAtUnix,
                });
                string json = JsonUtility.ToJson(new Envelope
                {
                    version = FormatVersion,
                    checksum = Sha256Hex(payload),
                    payload = payload,
                });

                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                File.WriteAllText(tmpPath, json, new UTF8Encoding(false));
                if (File.Exists(_path))
                {
                    File.Replace(tmpPath, _path, null);
                }
                else
                {
                    File.Move(tmpPath, _path);
                }
                return Result.Ok();
            }
            catch (Exception e) when (IsIoFailure(e))
            {
                TryDelete(tmpPath);
                return Result.Fail(ErrorCode.StorageFailure);
            }
        }

        public Result Delete()
        {
            try
            {
                if (File.Exists(_path))
                {
                    File.Delete(_path);
                }
                return Result.Ok();
            }
            catch (Exception e) when (IsIoFailure(e))
            {
                return Result.Fail(ErrorCode.StorageFailure);
            }
        }

        public Result Quarantine()
        {
            try
            {
                if (!File.Exists(_path))
                {
                    return Result.Ok();
                }
                string corruptPath = _path + ".corrupt";
                if (File.Exists(corruptPath))
                {
                    File.Delete(corruptPath);
                }
                File.Move(_path, corruptPath);
                return Result.Ok();
            }
            catch (Exception e) when (IsIoFailure(e))
            {
                return Result.Fail(ErrorCode.StorageFailure);
            }
        }

        private static bool IsIoFailure(Exception e)
        {
            return e is IOException || e is UnauthorizedAccessException || e is NotSupportedException;
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
                // 임시 파일 정리 실패는 무시한다. 다음 저장 때 덮어쓴다.
            }
        }

        /// <summary>깨진 JSON이면 null.</summary>
        private static T ParseOrNull<T>(string json) where T : class
        {
            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch (ArgumentException)
            {
                return null;
            }
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

        [Serializable]
        internal sealed class Envelope
        {
            public int version;
            public string checksum;
            public string payload;
        }

        [Serializable]
        internal sealed class ProfileDto
        {
            public string id;
            public string nickname;
            public long createdAtUnix;
        }
    }
}
