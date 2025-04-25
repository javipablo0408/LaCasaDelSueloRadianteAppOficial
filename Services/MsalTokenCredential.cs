using System.Threading;
using System.Threading.Tasks;
using Azure.Core;

namespace LaCasaDelSueloRadianteApp.Services
{
    // Conecta AuthenticationResult (MSAL) con Azure.Core TokenCredential
    public sealed class MsalTokenCredential : TokenCredential
    {
        private readonly MauiMsalAuthService _auth;

        public MsalTokenCredential(MauiMsalAuthService auth) => _auth = auth;

        public override AccessToken GetToken(TokenRequestContext ctx, CancellationToken ct)
        {
            var r = _auth.AcquireTokenAsync().GetAwaiter().GetResult();
            return new AccessToken(r.AccessToken, r.ExpiresOn);
        }

        public override async ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext ctx, CancellationToken ct)
        {
            var r = await _auth.AcquireTokenAsync();
            return new AccessToken(r.AccessToken, r.ExpiresOn);
        }
    }
}