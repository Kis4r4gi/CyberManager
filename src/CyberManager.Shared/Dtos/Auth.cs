namespace CyberManager.Shared.Dtos;

public record LoginRequest(string Username, string Password);
public record LoginResponse(string Token, int UserId, string Username, string FullName, UserRole Role, DateTime ExpiresUtc);

public record UserDto(int Id, string Username, string FullName, UserRole Role, bool Active, DateTime? LastLoginUtc);
public record CreateUserRequest(string Username, string Password, string FullName, UserRole Role);
public record UpdateUserRequest(string FullName, UserRole Role, bool Active, string? Password);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
