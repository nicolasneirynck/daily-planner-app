using Microsoft.AspNetCore.Components;
using Planning.Client.Http;
using Planning.Shared.Identity.Accounts;

namespace Planning.Client.Identity;

public partial class Register
{
    [Inject] public required IAccountManager AccountManager { get; set; }

    private ClientResult? _result;
    private AccountRequest.Register Model { get; set; } = new();

    public async Task RegisterUserAsync()
    {
        _result = await AccountManager.RegisterAsync(Model.Email!, Model.Password!, Model.ConfirmPassword!);

    }
}
