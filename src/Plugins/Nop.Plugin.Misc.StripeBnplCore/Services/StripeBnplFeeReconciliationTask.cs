using Nop.Services.ScheduleTasks;

namespace Nop.Plugin.Misc.StripeBnplCore.Services;

public sealed class StripeBnplFeeReconciliationTask : IScheduleTask
{
    private readonly IStripeBnplFeeReconciliationService _reconciliationService;

    public StripeBnplFeeReconciliationTask(IStripeBnplFeeReconciliationService reconciliationService)
    {
        _reconciliationService = reconciliationService;
    }

    public Task ExecuteAsync() => _reconciliationService.ReconcileAsync();
}
