namespace Pefi.Bank.CustomerPortal.Services;

public class CustomerState
{
    public Guid? CustomerId { get; set; }

    public bool IsLoggedIn => CustomerId.HasValue;

    public event Action? OnChange;

    public void SetCustomer(Guid id)
    {
        CustomerId = id;
        OnChange?.Invoke();
    }

    public void Clear()
    {
        CustomerId = null;
        OnChange?.Invoke();
    }
}
