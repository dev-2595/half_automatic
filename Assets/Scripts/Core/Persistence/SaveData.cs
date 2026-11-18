using System;

namespace HalfAutomatic.Core
{
    // 저장 파일용 데이터 형식. Unity JsonUtility가 읽을 수 있도록 [Serializable] + public 필드만 쓴다.
    // 필드 이름을 바꾸면 기존 저장 파일과 호환이 깨지므로 SaveData.CurrentVersion을 올리고 변환을 추가한다.

    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

        public int version;
        public long savedAtUnix;
        public string nickname;
        public int job;
        public int level;
        public long exp;
        public long coins;
        public long[] scrolls;
        public WeaponData[] weapons;
        public long equippedWeaponId;
        public long nextWeaponId;
        public int[] stats;
        public int tutorialIndex;
    }

    [Serializable]
    public sealed class WeaponData
    {
        public long id;
        public string definitionId;
        public int stars;
        public int level;
    }

    [Serializable]
    public sealed class AccountsData
    {
        public const int CurrentVersion = 1;

        public int version;
        public AccountData[] accounts;
    }

    [Serializable]
    public sealed class AccountData
    {
        public string username;
        public string salt;
        public string hash;
        public int iterations;
        public long createdAtUnix;
    }
}
