using System.Collections.Concurrent;

namespace Nop.Plugin.Misc.StripeBnplCore.Services;

// IIS runs this store in one worker. All three local order writers share the
// same gate; durable session/payment CAS claims remain the database safeguards.
internal static class StripeBnplOrderGate
{
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> Gates = new();
    internal static SemaphoreSlim Get(int orderId) => Gates.GetOrAdd(orderId, _ => new SemaphoreSlim(1, 1));
}
