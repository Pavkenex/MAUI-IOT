namespace MauiIot.Api.Models;

public sealed class UserDocument
{
    public string Id { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
