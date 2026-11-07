using System.Security.Claims;

namespace CyberManager.Server.Endpoints;

public static class ClaimsExtensions
{
    public static int UserId(this ClaimsPrincipal user)
        => int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public static string UserName(this ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.Name) ?? "sistema";
}

public static class ApiResults
{
    public static IResult Ok<T>(T value) => Results.Ok(value);
}
