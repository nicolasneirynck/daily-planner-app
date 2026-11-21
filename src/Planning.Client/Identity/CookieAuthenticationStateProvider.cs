using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Planning.Client.Http;
using Planning.Shared.Identity.Accounts;

namespace Planning.Client.Identity
{
    /// <summary>
    /// Handles state for cookie-based auth.
    /// </summary>
    /// <remarks>
    /// Create a new instance of the auth provider.
    /// </remarks>
    /// <param name="httpClientFactory">Factory to retrieve auth client.</param>
    public class CookieAuthenticationStateProvider(IHttpClientFactory httpClientFactory): AuthenticationStateProvider, IAccountManager
    {
        /// <summary>
        /// Special auth client.
        /// </summary>
        private readonly HttpClient httpClient = httpClientFactory.CreateClient("SecureApi");
        
        /// <summary>
        /// Authentication state.
        /// </summary>
        private bool authenticated = false;

        /// <summary>
        /// Default principal for anonymous (not authenticated) users.
        /// </summary>
        private readonly ClaimsPrincipal unauthenticated = new(new ClaimsIdentity());

        /// <summary>
        /// Register a new user.
        /// </summary>
        /// <param name="email">The user's email address.</param>
        /// <param name="password">The user's password.</param>
        /// <returns>The registration result.
        /// </returns>
        public async Task<ClientResult> RegisterAsync(string email, string password, string confirmPassword)
        {
            try
            {
                using var response = await httpClient.PostAsJsonAsync("/api/identity/accounts/register", new AccountRequest.Register
                {
                    Email = email,
                    Password = password,
                    ConfirmPassword = confirmPassword,
                });

                return await ApiResponseReader.ReadClientAsync(response);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not register user.");
                return new ClientResult(
                    HttpStatusCode.UnprocessableEntity,
                    "An unknown error prevented registration from succeeding.");
            }
         }

        /// <summary>
        /// User login.
        /// </summary>
        /// <param name="email">The user's email address.</param>
        /// <param name="password">The user's password.</param>
        /// <returns>The result of the login request.</returns>
        public async Task<ClientResult> LoginAsync(string email, string password)
        {
            try
            {
                using var response = await httpClient.PostAsJsonAsync("/api/identity/accounts/login", new AccountRequest.Login
                {
                    Email = email,
                    Password = password,
                });

                var result = await ApiResponseReader.ReadClientAsync(response);

                if (result.StatusCode == HttpStatusCode.Unauthorized)
                    return result with
                    {
                        ErrorMessage = "Invalid email and/or password."
                    };

                if (result.IsSuccess)
                {
                    NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
                }

                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not login user.");
            }

            return new ClientResult(
                HttpStatusCode.UnprocessableEntity,
                "Invalid email and/or password.");
        }

        /// <summary>
        /// Get authentication state.
        /// </summary>
        /// <remarks>
        /// Called by Blazor anytime and authentication-based decision needs to be made, then cached
        /// until the changed state notification is raised.
        /// </remarks>
        /// <returns>The authentication state asynchronous request.</returns>
        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            authenticated = false;
            // default to not authenticated
            var user = unauthenticated;

            try
            {
                using var response = await httpClient.GetAsync("/api/identity/accounts/info");
                var result =
                    await ApiResponseReader.ReadClientAsync<AccountResponse.Info>(response);

                if (result.StatusCode == HttpStatusCode.Unauthorized)
                    return new AuthenticationState(unauthenticated);

                if (result.IsSuccess && result.Value is { } info) // result.Value is { } info controleert dat Value niet null is en geeft die waarde tegelijk de lokale naam info
                {
                    var claims = new List<Claim>
                    {
                        new(ClaimTypes.Name, info.Email),
                        new(ClaimTypes.Email, info.Email)
                    };
                
                    claims.AddRange(
                        info.Claims
                            .Where(c => c.Key is not (ClaimTypes.Name or ClaimTypes.Email or ClaimTypes.Role))
                            .Select(c => new Claim(c.Key, c.Value))
                    );
                
                    claims.AddRange(info.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
                
                    var identity = new ClaimsIdentity(claims, nameof(CookieAuthenticationStateProvider));
                    user = new ClaimsPrincipal(identity);
                    authenticated = true;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not GetAuthenticationStateAsync.");
            }

            return new AuthenticationState(user);
        }

        public async Task<ClientResult> LogoutAsync()
        {
            try
            {
                using var response =
                    await httpClient.PostAsync("/api/identity/accounts/logout", null);
                var result = await ApiResponseReader.ReadClientAsync(response);

                if (result.IsSuccess)
                    NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not log out.");
                return new ClientResult(
                    HttpStatusCode.UnprocessableEntity,
                    "Could not log out. Please try again.");
            }
        }

        public async Task<bool> CheckAuthenticatedAsync()
        {
            await GetAuthenticationStateAsync();
            return authenticated;
        }
    }
}
