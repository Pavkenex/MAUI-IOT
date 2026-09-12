using MauiIot.Api.Models;
using Microsoft.AspNetCore.Identity;

namespace MauiIot.Api.Services;

public sealed class PasswordService
{
    private readonly PasswordHasher<UserDocument> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(new UserDocument(), password);

    public bool Verify(string passwordHash, string password)
    {
        var result = _hasher.VerifyHashedPassword(new UserDocument(), passwordHash, password);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
