using Stripe;
using Stripe.Checkout;

namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public interface IStripeBnplSessionClient
{
    Task<Session> CreateAsync(SessionCreateOptions options, RequestOptions requestOptions);
    Task<Session> GetAsync(string sessionId, SessionGetOptions options = null);
}
