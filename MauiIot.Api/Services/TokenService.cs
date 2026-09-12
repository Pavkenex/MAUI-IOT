using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MauiIot.Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace MauiIot.Api.Services;

public sealed class TokenService
{
    private const string SubClaim = "sub";
    private const string NameClaim = "name";

    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private readonly SymmetricSecurityKey _signingKey;
    private readonly TokenValidationParameters _validationParameters;

    public TokenService(string signingKey)
    {
        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        _validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    }

    public (string Token, DateTimeOffset ExpiresAtUtc) CreateToken(UserDocument user)
    {
        var expiresAtUtc = DateTimeOffset.UtcNow.Add(Lifetime);
        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims:
            [
                new Claim(SubClaim, user.Id),
                new Claim(NameClaim, user.Username),
            ],
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: credentials);

        var encoded = new JwtSecurityTokenHandler { MapInboundClaims = false }.WriteToken(token);
        return (encoded, expiresAtUtc);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            return handler.ValidateToken(token, _validationParameters, out _);
        }
        catch (SecurityTokenException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
