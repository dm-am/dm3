using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Identity;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace DM.Workers.SearchIndexer.Interceptors;

internal class IdentityInterceptor(IIdentitySetter identitySetter) : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        identitySetter.Current = Identity.Guest();
        var response = await continuation.Invoke(request, context);
        return response;
    }
}