namespace HalfAutomatic.Core
{
    public interface IClock
    {
        long UnixSeconds { get; }
    }

    /// <summary>
    /// 문자열 직렬화 추상화. 코어는 JSON 라이브러리에 의존하지 않고,
    /// Unity에서는 JsonUtility 구현을, 테스트에서는 다른 구현을 주입한다.
    /// </summary>
    public interface ISerializer<T> where T : class
    {
        string Serialize(T value);

        /// <summary>형식이 잘못되었으면 DataCorrupt.</summary>
        Result<T> Deserialize(string text);
    }
}
