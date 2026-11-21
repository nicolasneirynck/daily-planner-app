using Microsoft.AspNetCore.Components;

namespace Planning.Client.Identity;

public partial class Logout
{
    [Inject] public required IAccountManager AccountManager { get; set; }
    private string? _logoutError;

    protected override async Task OnInitializedAsync()
    {
        if (await AccountManager.CheckAuthenticatedAsync())
        {
            var result = await AccountManager.LogoutAsync();
            if (!result.IsSuccess)
                _logoutError = result.Errors.FirstOrDefault()
                    ?? result.ValidationErrors.FirstOrDefault()?.ErrorMessage
                    ?? "Could not log out. Please try again.";
        }
    }
}
