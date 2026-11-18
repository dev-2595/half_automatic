using System;
using System.Collections.Generic;

namespace HalfAutomatic.Core
{
    /// <summary>GameState ↔ SaveData 변환. 불러오기는 모든 값을 검증한 뒤 새 상태를 만든다(실패 시 아무것도 만들지 않음).</summary>
    public static class SaveMapper
    {
        public static SaveData ToData(GameState state, long savedAtUnix)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
            var weapons = new WeaponData[state.Weapons.Items.Count];
            for (int i = 0; i < weapons.Length; i++)
            {
                WeaponInstance w = state.Weapons.Items[i];
                weapons[i] = new WeaponData { id = w.Id, definitionId = w.DefinitionId, stars = w.Stars, level = w.Level };
            }
            return new SaveData
            {
                version = SaveData.CurrentVersion,
                savedAtUnix = savedAtUnix,
                nickname = state.Nickname,
                job = (int)state.Job,
                level = state.Level.Level,
                exp = state.Level.Exp,
                coins = state.Wallet.Coins,
                scrolls = state.Wallet.ScrollsSnapshot(),
                weapons = weapons,
                equippedWeaponId = state.Weapons.Equipped == null ? 0 : state.Weapons.Equipped.Id,
                nextWeaponId = state.Weapons.NextId,
                stats = new[]
                {
                    state.Stats.GetLevel(StatType.Health),
                    state.Stats.GetLevel(StatType.Attack),
                    state.Stats.GetLevel(StatType.Cooldown),
                },
                tutorialIndex = state.Tutorial.CurrentIndex,
            };
        }

        /// <summary>
        /// 저장 데이터로 상태를 만든다. 더 새로운 버전이면 UnsupportedVersion, 그 밖의 잘못된 값은 DataCorrupt.
        /// </summary>
        public static Result<GameState> FromData(SaveData data, GameRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }
            if (data == null)
            {
                return Corrupt();
            }
            if (data.version > SaveData.CurrentVersion)
            {
                return Result<GameState>.Fail(ErrorCode.UnsupportedVersion);
            }
            if (data.version < 1)
            {
                return Corrupt();
            }

            // JsonUtility는 null 문자열을 빈 문자열로 읽으므로 빈 문자열도 "닉네임 없음"으로 취급한다.
            string nickname = string.IsNullOrEmpty(data.nickname) ? null : data.nickname;
            if (nickname != null)
            {
                Result<string> valid = NicknameRules.Validate(nickname);
                if (!valid.IsOk || valid.Value != nickname)
                {
                    return Corrupt();
                }
            }

            var job = (JobClass)data.job;
            if (!JobRules.IsDefined(job))
            {
                return Corrupt();
            }

            var level = new PlayerLevel(rules.Curve);
            if (!level.Restore(data.level, data.exp).IsOk)
            {
                return Corrupt();
            }

            var wallet = new Wallet();
            if (!wallet.Restore(data.coins, data.scrolls).IsOk)
            {
                return Corrupt();
            }

            var weapons = new WeaponInventory(rules.Catalog);
            if (data.weapons == null)
            {
                return Corrupt();
            }
            var snapshots = new List<WeaponSnapshot>(data.weapons.Length);
            foreach (WeaponData w in data.weapons)
            {
                if (w == null)
                {
                    return Corrupt();
                }
                snapshots.Add(new WeaponSnapshot(w.id, w.definitionId, w.stars, w.level));
            }
            if (!weapons.Restore(snapshots, data.equippedWeaponId, data.nextWeaponId, rules.Config.MaxStars,
                    rules.Config.MaxWeaponLevel).IsOk)
            {
                return Corrupt();
            }

            var stats = new StatSheet();
            if (data.stats == null || data.stats.Length != StatSheet.StatCount
                || !stats.Restore(data.stats[0], data.stats[1], data.stats[2], data.level).IsOk)
            {
                return Corrupt();
            }

            var tutorial = new TutorialRunner(rules.TutorialSteps);
            if (!tutorial.Restore(data.tutorialIndex).IsOk)
            {
                return Corrupt();
            }

            return Result<GameState>.Ok(new GameState(level, wallet, weapons, stats, tutorial, nickname, job));
        }

        private static Result<GameState> Corrupt() => Result<GameState>.Fail(ErrorCode.DataCorrupt);
    }
}
