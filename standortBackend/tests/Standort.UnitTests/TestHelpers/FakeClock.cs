using Standort.Application.Interfaces;

namespace Standort.UnitTests.TestHelpers;

public sealed class FakeClock : ISystemClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 5, 5, 10, 0, 0, TimeSpan.Zero);
}
