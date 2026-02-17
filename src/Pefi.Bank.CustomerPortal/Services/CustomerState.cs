using Microsoft.JSInterop;

namespace Pefi.Bank.CustomerPortal.Services;

public class CustomerState
{
    private readonly IJSRuntime _js;

    public CustomerState(IJSRuntime js)
    {
        _js = js;
    }

    public Guid? CustomerId { get; set; }
    public string? Token { get; set; }
    public string? Email { get; set; }

    public bool IsLoggedIn => CustomerId.HasValue && !string.IsNullOrEmpty(Token);

    public event Action? OnChange;

    public void SetAuth(Guid customerId, string token, string email)
    {
        CustomerId = customerId;
        Token = token;
        Email = email;
        OnChange?.Invoke();
        _ = PersistAsync();
    }

    public void Clear()
    {
        CustomerId = null;
        Token = null;
        Email = null;
        OnChange?.Invoke();
        _ = ClearStorageAsync();
    }

    public async Task RestoreAsync()
    {
        try
        {
            var token = await _js.InvokeAsync<string?>("localStorage.getItem", "pefi_token");
            var customerId = await _js.InvokeAsync<string?>("localStorage.getItem", "pefi_customerId");
            var email = await _js.InvokeAsync<string?>("localStorage.getItem", "pefi_email");

            if (!string.IsNullOrEmpty(token) && Guid.TryParse(customerId, out var id))
            {
                CustomerId = id;
                Token = token;
                Email = email;
                OnChange?.Invoke();
            }
        }
        catch
        {
            // localStorage may not be available during prerendering
        }
    }

    private async Task PersistAsync()
    {
        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", "pefi_token", Token ?? "");
            await _js.InvokeVoidAsync("localStorage.setItem", "pefi_customerId", CustomerId?.ToString() ?? "");
            await _js.InvokeVoidAsync("localStorage.setItem", "pefi_email", Email ?? "");
        }
        catch
        {
            // Silently ignore
        }
    }

    private async Task ClearStorageAsync()
    {
        try
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", "pefi_token");
            await _js.InvokeVoidAsync("localStorage.removeItem", "pefi_customerId");
            await _js.InvokeVoidAsync("localStorage.removeItem", "pefi_email");
        }
        catch
        {
            // Silently ignore
        }
    }
}
