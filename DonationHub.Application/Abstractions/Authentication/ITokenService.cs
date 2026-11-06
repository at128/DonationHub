namespace DonationHub.Application.Abstractions.Authentication;

public interface ITokenService
{
    public string CreateAccessToken(Guid userId,string email,IEnumerable<string> roles);
    public string GenerateRefreshToken();
    public string HashRefreshToken(string token);
}