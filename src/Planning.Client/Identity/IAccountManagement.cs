using Planning.Client.Http;

namespace Planning.Client.Identity
{
    /// <summary>
    /// Account management services.
    /// </summary>
    public interface IAccountManager
    {
        /// <summary>
        /// Login service.
        /// </summary>
        /// <param name="email">User's email.</param>
        /// <param name="password">User's password.</param>
        /// <returns>The result of the login request.</returns>
        public Task<ClientResult> LoginAsync(string email, string password);

        /// <summary>
        /// Log out the logged in user.
        /// </summary>
        /// <returns>The result of the logout request.</returns>
        public Task<ClientResult> LogoutAsync();

        /// <summary>
        /// Registration service.
        /// </summary>
        /// <param name="email">User's email.</param>
        /// <param name="password">User's password.</param>
        /// <returns>The result of the registration request.</returns>
        public Task<ClientResult> RegisterAsync(string email, string password, string confirmPassword);

        public Task<bool> CheckAuthenticatedAsync();
    }
}
