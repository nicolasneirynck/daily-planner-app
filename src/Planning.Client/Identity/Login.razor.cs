using Microsoft.AspNetCore.Components;
using Planning.Shared.Identity.Accounts;

namespace Planning.Client.Identity;

public partial class Login
{
    [SupplyParameterFromQuery] private string? ReturnUrl { get; set; }

    private AccountRequest.Login Model = new();
    private Result _result = new();
    [Inject] public required IAccountManager AccountManager { get; set; }
    [Inject] public required NavigationManager Navigation { get; set; }
    public async Task LoginUser()
    {
        _result = await AccountManager.LoginAsync(Model.Email!, Model.Password!);

        if (_result.IsSuccess && !string.IsNullOrEmpty(ReturnUrl) )
        {
            var destination = string.IsNullOrWhiteSpace(ReturnUrl)
                ? "/dayplanning"
                : ReturnUrl;
            
            Navigation.NavigateTo(destination);
        }
    }
}