using Nop.Services.ScheduleTasks;

namespace Nop.Plugin.Misc.HoodWebpOptimizer.Services;

public sealed class WebpOptimizationTask : IScheduleTask
{
    private readonly HoodWebpSettings _settings;
    private readonly IExistingPictureOptimizer _optimizer;

    public WebpOptimizationTask(HoodWebpSettings settings, IExistingPictureOptimizer optimizer)
    {
        _settings = settings;
        _optimizer = optimizer;
    }

    public async Task ExecuteAsync()
    {
        if (_settings.Enabled && _settings.ProcessExistingPictures)
            await _optimizer.ProcessBatchAsync(_settings.ExistingBatchSize);
    }
}
