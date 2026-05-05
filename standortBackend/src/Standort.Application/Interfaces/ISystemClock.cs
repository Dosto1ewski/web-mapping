namespace Standort.Application.Interfaces;

public interface ISystemClock
{
    DateTimeOffset UtcNow { get; }
}
