using System;
using HalfAutomatic.Core;
using UnityEngine;

namespace HalfAutomatic.Platform
{
    /// <summary>Unity JsonUtility 기반 직렬화기. 형식이 깨진 JSON은 DataCorrupt로 돌려준다.</summary>
    public sealed class JsonUtilitySerializer<T> : ISerializer<T> where T : class
    {
        public string Serialize(T value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            return JsonUtility.ToJson(value);
        }

        public Result<T> Deserialize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Result<T>.Fail(ErrorCode.DataCorrupt);
            }
            try
            {
                T value = JsonUtility.FromJson<T>(text);
                return value == null ? Result<T>.Fail(ErrorCode.DataCorrupt) : Result<T>.Ok(value);
            }
            catch (ArgumentException)
            {
                return Result<T>.Fail(ErrorCode.DataCorrupt);
            }
        }
    }
}
