using Microsoft.Identity.Client;

namespace LaCasaDelSueloRadianteApp.Services
{
    public class MauiMsalAuthService
    {
        private readonly IPublicClientApplication _pca;
        private readonly string[] _scopes = new[]
        {
            "Files.Read",
            "Files.ReadWrite",
            "Files.Read.All",
            "Files.ReadWrite.All",
            "offline_access"
        };

        public MauiMsalAuthService()
        {
            var builder = PublicClientApplicationBuilder
                .Create("30af0f82-bbeb-4f49-89cd-3ff526bc339b")
                .WithRedirectUri("http://localhost")
                .WithAuthority(AadAuthorityAudience.AzureAdAndPersonalMicrosoftAccount);

#if ANDROID
            builder = builder.WithParentActivityOrWindow(() => Platform.CurrentActivity);
#endif

            _pca = builder.Build();
        }

        public async Task<AuthenticationResult> AcquireTokenAsync()
        {
            try
            {
                var accounts = await _pca.GetAccountsAsync();
                var firstAccount = accounts.FirstOrDefault();

                if (firstAccount != null)
                {
                    return await _pca.AcquireTokenSilent(_scopes, firstAccount)
                        .ExecuteAsync();
                }

                return await AcquireTokenInteractiveAsync();
            }
            catch (MsalUiRequiredException)
            {
                return await AcquireTokenInteractiveAsync();
            }
        }

        private async Task<AuthenticationResult> AcquireTokenInteractiveAsync()
        {
            var builder = _pca.AcquireTokenInteractive(_scopes)
                .WithPrompt(Prompt.SelectAccount);

#if ANDROID
            builder = builder.WithParentActivityOrWindow(Platform.CurrentActivity);
#endif

            return await builder.ExecuteAsync();
        }

        public async Task SignOutAsync()
        {
            var accounts = await _pca.GetAccountsAsync();
            foreach (var account in accounts)
            {
                await _pca.RemoveAsync(account);
            }
        }
    }
}