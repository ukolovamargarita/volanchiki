using Volanchiki.Api.Domain;

namespace Volanchiki.Tests;

static class TestData
{
    public static NewsItem News(
        string id,
        string title = "Заголовок",
        string summary = "",
        DateTimeOffset? published = null,
        bool seed = false,
        string category = "Прочее",
        bool needsLlm = false,
        bool business = false) => new()
    {
        NeedsLlm = needsLlm,
        BusinessRelevant = business,
        Id = id,
        Title = title,
        Summary = summary,
        PublishedAt = published ?? DateTimeOffset.UtcNow,
        IsSeed = seed,
        Category = category,
        Source = "test",
    };

    public static Company Co(
        string id,
        string clientType = "CLIENT",
        decimal? revenue = null,
        string? group = null,
        bool loan = false,
        string? okved = null,
        string? city = null) => new()
    {
        Id = id,
        ClientType = clientType,
        Revenue = revenue,
        GroupId = group,
        HasLoan = loan,
        OkvedName = okved,
        City = city,
    };

    /// <summary>Ищет папку read_only/from_alfa, поднимаясь вверх от exe и от текущей папки.</summary>
    public static string? FindAlfaDir()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = new DirectoryInfo(start);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "read_only", "from_alfa");
                if (Directory.Exists(candidate))
                    return candidate;
                dir = dir.Parent;
            }
        }

        return null;
    }
}
