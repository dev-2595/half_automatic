using System;
using System.Collections.Generic;
using System.Linq;

namespace HalfAutomatic.Core
{
    /// <summary>[합성] 버튼 결과.</summary>
    public sealed class SynthesisReport
    {
        public IReadOnlyList<WeaponInstance> Created { get; }
        public int ConsumedCount { get; }

        internal SynthesisReport(IReadOnlyList<WeaponInstance> created, int consumedCount)
        {
            Created = created;
            ConsumedCount = consumedCount;
        }
    }

    /// <summary>
    /// 자동 합성: 같은 무기·같은 성급 2개 → 성급 +1 (최대 MaxStars).
    /// 규칙(⚠️ 기획 확정 전 임시):
    ///  - 장착 중인 무기는 재료로 쓰지 않는다.
    ///  - 결과 무기의 레벨은 두 재료 중 높은 레벨을 유지한다.
    ///  - 낮은 성급부터 처리하므로 1성 4개 → 2성 2개 → 3성 1개처럼 연쇄 합성된다.
    ///  - 같은 조건이면 레벨이 높은 것, 그다음 먼저 얻은 것부터 짝짓는다(결과가 항상 같다).
    /// </summary>
    public sealed class WeaponSynthesizer
    {
        private readonly BalanceConfig _config;

        public WeaponSynthesizer(BalanceConfig config)
        {
            _config = BalanceConfig.RequireValid(config);
        }

        public SynthesisReport AutoSynthesize(WeaponInventory inventory)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            var created = new List<WeaponInstance>();
            int consumed = 0;
            for (int stars = 1; stars < _config.MaxStars; stars++)
            {
                IEnumerable<IGrouping<string, WeaponInstance>> groups = inventory.Items
                    .Where(w => w.Stars == stars && w != inventory.Equipped)
                    .GroupBy(w => w.DefinitionId, StringComparer.Ordinal)
                    .OrderBy(g => g.Key, StringComparer.Ordinal)
                    .ToList();

                foreach (IGrouping<string, WeaponInstance> group in groups)
                {
                    List<WeaponInstance> ordered = group
                        .OrderByDescending(w => w.Level)
                        .ThenBy(w => w.Id)
                        .ToList();
                    for (int i = 0; i + 1 < ordered.Count; i += 2)
                    {
                        WeaponInstance a = ordered[i];
                        WeaponInstance b = ordered[i + 1];
                        if (!inventory.RemoveInternal(a) || !inventory.RemoveInternal(b))
                        {
                            // 위에서 장착 무기를 걸렀으므로 일어나지 않아야 한다.
                            throw new InvalidOperationException("synthesis material could not be removed");
                        }
                        consumed += 2;
                        created.Add(inventory.AddInternal(group.Key, stars + 1, Math.Max(a.Level, b.Level)));
                    }
                }
            }
            // 연쇄 합성으로 중간 결과물이 다시 재료로 쓰였으면 최종 목록에서 뺀다.
            List<WeaponInstance> survivors = created.Where(w => inventory.Find(w.Id) != null).ToList();
            return new SynthesisReport(survivors.AsReadOnly(), consumed);
        }
    }
}
