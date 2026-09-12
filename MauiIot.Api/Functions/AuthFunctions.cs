using System.Net;
using System.Text.RegularExpressions;
using MauiIot.Api.Models;
using MauiIot.Api.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace MauiIot.Api.Functions;

public sealed partial class AuthFunctions
{
    private const int MinPasswordLength = 8;

    private readonly CosmosStore _store;
    private readonly PasswordService _passwords;
    private readonly TokenService _tokens;

    public AuthFunctions(CosmosStore store, PasswordService passwords, TokenService tokens)
    {
        _store = store;
        _passwords = passwords;
        _tokens = tokens;
    }

    [Function("Register")]
    public async Task<HttpResponseData> RegisterAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/register")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var body = await HttpHelpers.ReadBodyAsync<RegisterRequest>(request);
        var username = Normalize(body?.Username);
        var password = body?.Password ?? string.Empty;

        if (username is null || !UsernamePattern().IsMatch(username))
        {
            return await HttpHelpers.JsonAsync(
                request,
                HttpStatusCode.BadRequest,
                new ErrorResponse { Error = "Username must be 3-32 characters using letters, digits, dot, underscore or hyphen." },
                cancellationToken);
        }

        if (password.Length < MinPasswordLength)
        {
            return await HttpHelpers.JsonAsync(
                request,
                HttpStatusCode.BadRequest,
                new ErrorResponse { Error = $"Password must be at least {MinPasswordLength} characters." },
                cancellationToken);
        }

        var user = new UserDocument
        {
            Id = username,
            Username = body!.Username!.Trim(),
            PasswordHash = _passwords.Hash(password),
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        if (!await _store.CreateUserAsync(user, cancellationToken))
        {
            return await HttpHelpers.JsonAsync(
                request,
                HttpStatusCode.Conflict,
                new ErrorResponse { Error = "Username is already taken." },
                cancellationToken);
        }

        return await HttpHelpers.JsonAsync(request, HttpStatusCode.Created, BuildAuthResponse(user), cancellationToken);
    }

    [Function("Login")]
    public async Task<HttpResponseData> LoginAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/login")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var body = await HttpHelpers.ReadBodyAsync<LoginRequest>(request);
        var username = Normalize(body?.Username);
        var password = body?.Password ?? string.Empty;

        var user = username is null ? null : await _store.GetUserAsync(username, cancellationToken);

        if (user is null || !_passwords.Verify(user.PasswordHash, password))
        {
            return await HttpHelpers.JsonAsync(
                request,
                HttpStatusCode.Unauthorized,
                new ErrorResponse { Error = "Invalid username or password." },
                cancellationToken);
        }

        return await HttpHelpers.JsonAsync(request, HttpStatusCode.OK, BuildAuthResponse(user), cancellationToken);
    }

    private AuthResponse BuildAuthResponse(UserDocument user)
    {
        var (token, expiresAtUtc) = _tokens.CreateToken(user);
        return new AuthResponse
        {
            Token = token,
            ExpiresAtUtc = expiresAtUtc,
            Username = user.Username,
        };
    }

    private static string? Normalize(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        return username.Trim().ToLowerInvariant();
    }

    [GeneratedRegex("^[a-z0-9._-]{3,32}$")]
    private static partial Regex UsernamePattern();
}
