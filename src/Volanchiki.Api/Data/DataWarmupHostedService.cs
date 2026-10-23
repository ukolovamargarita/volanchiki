using Microsoft.Extensions.Options;
using Volanchiki.Api.Options;

namespace Volanchiki.Api.Data;

public class DataWarmupHostedService : IHostedService
{
    readonly IServiceProvider _sp;
    readonly DataPathsOptions _paths;
    readonly ILogger<DataWarmupHostedService> _log;

    public DataWarmupHostedService(
        IServiceProvider sp,
        IOptions<DataPathsOptions> paths,
        ILogger<DataWarmupHostedService> log)
    {
        _sp = sp;
        _paths = paths.Value;
        _log = log;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _sp.CreateScope();
        var sp = scope.ServiceProvider;

        try
        {
            var companyDir = PathResolver.FindDir(_paths.CompanyDir, "company");
            _log.LogInformation("load csv from {Dir}", companyDir);
            await sp.GetRequiredService<CsvCompanyLoader>()
                .LoadIntoAsync(sp.GetRequiredService<CompanyStore>(), companyDir, cancellationToken);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "csv load failed");
        }

        try
        {
            var alfaDir = PathResolver.FindDir(_paths.AlfaDir, "from_alfa");
            _log.LogInformation("load alfa txt from {Dir}", alfaDir);
            await sp.GetRequiredService<AlfaTxtLoader>()
                .LoadIntoAsync(sp.GetRequiredService<AlfaAnalyticsStore>(), alfaDir, cancellationToken);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "alfa txt load failed");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
