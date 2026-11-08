namespace HalfAutomatic.Core
{
    /// <summary>플레이어가 가진 무기 한 자루. 성급/레벨 변경은 코어 내부 서비스만 할 수 있다.</summary>
    public sealed class WeaponInstance
    {
        public long Id { get; }
        public string DefinitionId { get; }
        /// <summary>1 ~ MaxStars.</summary>
        public int Stars { get; internal set; }
        /// <summary>1 ~ MaxWeaponLevel.</summary>
        public int Level { get; internal set; }

        internal WeaponInstance(long id, string definitionId, int stars, int level)
        {
            Id = id;
            DefinitionId = definitionId;
            Stars = stars;
            Level = level;
        }
    }
}
