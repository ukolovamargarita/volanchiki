using System.Text;

namespace Volanchiki.Api.Data;

public class AlfaTxtLoader
{
    readonly ILogger<AlfaTxtLoader> _log;

    public AlfaTxtLoader(ILogger<AlfaTxtLoader> log) => _log = log;

    public async Task LoadIntoAsync(AlfaAnalyticsStore store, string dir, CancellationToken ct)
    {
        if (!Directory.Exists(dir))
            throw new DirectoryNotFoundException(dir);

        var files = Directory.GetFiles(dir, "*.txt");
        var docs = new List<AlfaDoc>();

        foreach (var path in files)
        {
            ct.ThrowIfCancellationRequested();
            var text = await File.ReadAllTextAsync(path, Encoding.UTF8, ct);
            text = text.Trim();
            if (text.Length == 0)
                continue;

            var name = Path.GetFileName(path);
            docs.Add(new AlfaDoc
            {
                FileName = name,
                Title = FirstLine(text) ?? Path.GetFileNameWithoutExtension(name),
                Text = text,
            });
        }

        store.ReplaceAll(docs);
        _log.LogInformation("alfa txt loaded: {N} files from {Dir}", docs.Count, dir);
    }

    static string? FirstLine(string text)
    {
        using var reader = new StringReader(text);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (line.Length > 0)
                return line.Length > 160 ? line.Substring(0, 160) : line;
        }

        return null;
    }
}
