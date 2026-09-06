using Nop.Services.ScheduleTasks;

namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public sealed class StripeBnplPendingOrderCleanupTask : IScheduleTask
{
    private readonly IStripeBnplPendingOrderCleanupService _cleanupService;

    public StripeBnplPendingOrderCleanupTask(IStripeBnplPendingOrderCleanupService cleanupService)
    {
        _cleanupService = cleanupService;
    }

    public async Task ExecuteAsync()
    {
        await _cleanupService.SweepAsync(100);
    }
}
