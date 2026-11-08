using System;

namespace HalfAutomatic.Core
{
    /// <summary>base × x^exponent 형태의 비용 곡선 계산. 결과는 반올림한 정수이며 long 범위를 넘으면 long.MaxValue로 포화한다.</summary>
    internal static class CostCurve
    {
        public static long Evaluate(double baseValue, double exponent, int x)
        {
            if (x < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(x));
            }
            double value = Math.Round(baseValue * Math.Pow(x, exponent), MidpointRounding.AwayFromZero);
            if (double.IsNaN(value) || value <= 0.0)
            {
                return 0;
            }
            // long.MaxValue는 double로 정확히 표현되지 않으므로 9.2e18 이상은 포화 처리한다.
            return value >= 9.2e18 ? long.MaxValue : (long)value;
        }
    }
}
