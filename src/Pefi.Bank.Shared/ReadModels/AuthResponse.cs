namespace Pefi.Bank.Shared.ReadModels;

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string Email { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
