using System;
using System.Collections.Generic;

namespace HalfAutomatic.Core
{
    /// <summary>게임 규칙 데이터 묶음(밸런스, 무기 목록, 경험치 곡선, 튜토리얼 스크립트). 상태를 갖지 않는다.</summary>
    public sealed class GameRules
    {
        public BalanceConfig Config { get; }
        public WeaponCatalog Catalog { get; }
        public ExperienceCurve Curve { get; }
        public IReadOnlyList<TutorialStep> TutorialSteps { get; }

        public GameRules(BalanceConfig config, WeaponCatalog catalog, IReadOnlyList<TutorialStep> tutorialSteps)
        {
            Config = BalanceConfig.RequireValid(config);
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            TutorialSteps = tutorialSteps ?? throw new ArgumentNullException(nameof(tutorialSteps));
            Curve = new ExperienceCurve(config);
            new TutorialRunner(tutorialSteps); // 스크립트 유효성 검사(잘못되면 예외)
        }

        public static GameRules CreateDefault()
        {
            return new GameRules(BalanceConfig.CreateDefault(), WeaponCatalog.CreateDefault(),
                TutorialScript.CreateDefault());
        }
    }
}
