using System.Net.Http.Headers;

namespace Pefi.Bank.CustomerPortal.Services;

public class AuthTokenHandler : DelegatingHandler
{
    private readonly CustomerState _state;

    public AuthTokenHandler(CustomerState state)
    {
        _state = state;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_state.Token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _state.Token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
