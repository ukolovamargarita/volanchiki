using Microsoft.Extensions.Logging.Abstractions;
using Volanchiki.Api.Data;
using Xunit;

namespace Volanchiki.Tests;

public class AlfaTxtLoaderTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "volanchiki-tests-" + Guid.NewGuid().ToString("N"));
    readonly AlfaTxtLoader _loader = new(NullLogger<AlfaTxtLoader>.Instance);

    public AlfaTxtLoaderTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    [Fact]
    public async Task Loads_txt_files_and_takes_title_from_first_non_empty_line()
    {
        File.WriteAllText(Path.Combine(_dir, "a.txt"), "\n\n  Заголовок отчёта  \nТекст отчёта");
        var store = new AlfaAnalyticsStore();

        await _loader.LoadIntoAsync(store, _dir, CancellationToken.None);

        Assert.Equal(1, store.DocCount);
        Assert.Equal("Заголовок отчёта", store.All()[0].Title);
        Assert.Equal("a.txt", store.All()[0].FileName);
    }

    [Fact]
    public async Task Skips_empty_files_and_non_txt_files()
    {
        File.WriteAllText(Path.Combine(_dir, "empty.txt"), "   \n  ");
        File.WriteAllText(Path.Combine(_dir, "notes.md"), "не txt");
        File.WriteAllText(Path.Combine(_dir, "ok.txt"), "Нормальный файл");
        var store = new AlfaAnalyticsStore();

        await _loader.LoadIntoAsync(store, _dir, CancellationToken.None);

        Assert.Equal(1, store.DocCount);
        Assert.Equal("ok.txt", store.All()[0].FileName);
    }

    [Fact]
    public async Task Long_first_line_is_cut_to_160_chars_in_title()
    {
        File.WriteAllText(Path.Combine(_dir, "long.txt"), new string('я', 300) + "\nтекст");
        var store = new AlfaAnalyticsStore();

        await _loader.LoadIntoAsync(store, _dir, CancellationToken.None);

        Assert.Equal(160, store.All()[0].Title.Length);
    }

    [Fact]
    public async Task Missing_directory_throws()
    {
        var store = new AlfaAnalyticsStore();

        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => _loader.LoadIntoAsync(store, Path.Combine(_dir, "nope"), CancellationToken.None));
    }

    // Проверка на настоящих материалах банка из репозитория
    [Fact]
    public async Task Real_bank_analytics_load_and_rate_query_finds_policy_rate_report()
    {
        var dir = TestData.FindAlfaDir();
        Assert.NotNull(dir); // если упало здесь — тест запущен вне репозитория, папка read_only не найдена

        var expectedFiles = Directory.GetFiles(dir!, "*.txt")
            .Count(f => File.ReadAllText(f).Trim().Length > 0);
        var store = new AlfaAnalyticsStore();

        await _loader.LoadIntoAsync(store, dir!, CancellationToken.None);

        Assert.True(expectedFiles > 0);
        Assert.Equal(expectedFiles, store.DocCount);

        var top = store.Search("ключевая ставка")[0];
        Assert.Contains("Policy_Rate", top.FileName);
    }
}
