namespace Pefi.Bank.Auth;

public class JwtSettings
{
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "PefiBank";
    public string Audience { get; set; } = "PefiBankCustomers";
    public int ExpiryMinutes { get; set; } = 480;
}
