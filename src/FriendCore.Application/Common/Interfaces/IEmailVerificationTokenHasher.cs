namespace FriendCore.Application.Common.Interfaces;

public interface IEmailVerificationTokenHasher
{
    string Hash(string token);

    string GenerateToken();
}