﻿using Elektrifikatsiya.Database;
using Elektrifikatsiya.Models;
using Elektrifikatsiya.Utilities;

using FluentResults;

using Microsoft.EntityFrameworkCore;

using BC = BCrypt.Net.BCrypt;

namespace Elektrifikatsiya.Services.Implementations;

public class AuthenticationService : IAuthenticationService
{
    private readonly UserDatabaseContext userDatabaseContext;
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly ICookieService cookieService;

    public AuthenticationService(UserDatabaseContext userDatabaseContext, IHttpContextAccessor httpContextAccessor, ICookieService cookieService)
    {
        this.userDatabaseContext = userDatabaseContext;
        this.httpContextAccessor = httpContextAccessor;
        this.cookieService = cookieService;
    }

    public async Task<Result> DeleteUserAsync()
    {
        Result<User> getUserResult = await GetUserAsync();

        if (getUserResult.IsFailed)
        {
            return getUserResult.ToResult();
        }

        _ = userDatabaseContext.Users.Remove(getUserResult.Value);
        return (await Result.Try(() => userDatabaseContext.SaveChangesAsync())).ToResult();
    }

    public async Task<Result<User>> GetUserAsync()
    {
        string? token = httpContextAccessor.HttpContext?.Request.Cookies["token"]?.ToString();

        if (!TokenGenerator.ValidateToken(token, "auth"))
        {
            return Result.Fail("Token is not valid!");
        }

        User? user = await userDatabaseContext.Users.FirstOrDefaultAsync(u => u.SessionToken == token);

        if (user is null || DateTime.UtcNow - user.LastLoginDate > TimeSpan.FromDays(7))
        {
            return Result.Fail("Token is not valid or expired!");
        }

        return user;
    }

    public async Task<Result<bool>> IsAuthenticated()
    {
        return (await GetUserAsync()).IsSuccess;
    }

    public async Task<Result> LoginUserAsync(string name, string password)
    {
        User? user = await userDatabaseContext.Users.FirstOrDefaultAsync(u => u.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));

        if (user is null)
        {
            return Result.Fail("User does not exist!");
        }

        if (!BC.Verify(password, user.PasswordHash))
        {
            return Result.Fail("Invalid credentials!");
        }

        string newToken = TokenGenerator.GenerateToken("auth", user.Id);

        user.LastLoginDate = DateTime.UtcNow;
        user.SessionToken = newToken;

        _ = await userDatabaseContext.SaveChangesAsync();

        await cookieService.WriteCookieAsync("token", newToken, 7);

        return Result.Ok();
    }

    public async Task<Result> LogoutUserAsync()
    {
        string? token = httpContextAccessor.HttpContext?.Request.Cookies["token"]?.ToString();

        await cookieService.WriteCookieAsync("token", string.Empty, 0);

        if (!TokenGenerator.ValidateToken(token, "auth"))
        {
            return Result.Ok();
        }

        User? user = await userDatabaseContext.Users.FirstOrDefaultAsync(u => u.SessionToken == token);

        if (user is null)
        {
            return Result.Ok();
        }

        user.SessionToken = null;

        _ = await userDatabaseContext.SaveChangesAsync();

        return Result.Ok();
    }

    public async Task<Result> RegisterUserAsync(string name, string password, Role role)
    {
        Result<bool> userExistsResult = await UserExistsAsync(name);

        if (userExistsResult.IsFailed || userExistsResult.Value)
        {
            return Result.Fail("User name already exists!");
        }

        User user = new User(name, BC.HashPassword(password), role);
        _ = userDatabaseContext.Users.Add(user);

        return (await Result.Try(() => userDatabaseContext.SaveChangesAsync())).ToResult();
    }

    public Task<Result<bool>> UserExistsAsync(string name)
    {
        return Result.Try(() =>
            userDatabaseContext.Users.AnyAsync(u => u.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase)));
    }
}