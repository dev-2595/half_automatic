using System;
using HalfAutomatic.Core;

namespace HalfAutomatic.Platform
{
    public sealed class SystemClock : IClock
    {
        public long UnixSeconds => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
