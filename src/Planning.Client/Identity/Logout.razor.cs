using Microsoft.AspNetCore.Components;

namespace Planning.Client.Identity;

public partial class Logout
{
    [Inject] public required IAccountManager AccountManager { get; set; }
    protected override async Task OnInitializedAsync()
    {
        if (await AccountManager.CheckAuthenticatedAsync())
        {
            await AccountManager.LogoutAsync();
        }
    }
}