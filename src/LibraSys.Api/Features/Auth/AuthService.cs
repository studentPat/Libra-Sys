using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LibraSys.Api.Configuration;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LibraSys.Api.Features.Auth;

public sealed class AuthService(
    AuthRepository repository,
    IOptions<JwtOptions> jwtOptions,
    IPasswordHasher<AuthUser> passwordHasher)
{
    public async Task<LoginResponse?> LoginAsync(
        LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await repository.FindActiveUserAsync(request.Username, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var verification = passwordHasher.VerifyHashedPassword(
            user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return null;
        }

        var options = jwtOptions.Value;
        if (string.IsNullOrWhiteSpace(options.SigningKey) || options.SigningKey.Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey must be configured with at least 32 characters.");
        }

        var expiresIn = TimeSpan.FromMinutes(options.ExpirationMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            new Claim(ClaimTypes.Role, user.RoleName)
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(expiresIn),
            signingCredentials: credentials);

        return new LoginResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            "Bearer",
            (int)expiresIn.TotalSeconds,
            user.Username,
            user.RoleName);
    }
}
