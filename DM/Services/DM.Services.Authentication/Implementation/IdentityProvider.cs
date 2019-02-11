using DM.Services.Authentication.Dto;

namespace DM.Services.Authentication.Implementation
{
    public class IdentityProvider : IIdentitySetter, IIdentityProvider
    {
        public AuthenticationResult Current { get; set; }
    }
}