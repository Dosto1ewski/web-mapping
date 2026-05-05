namespace Standort.Application.Interfaces;

public interface ITokenHasher
{
    string Hash(string plainToken);
    bool Verify(string plainToken, string storedHash);
}
