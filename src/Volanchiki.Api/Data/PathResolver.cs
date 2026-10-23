namespace Volanchiki.Api.Data;

static class PathResolver
{
    public static string FindDir(string configured, string leaf)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            foreach (var raw in new[]
                     {
                         configured,
                         Path.Combine(Directory.GetCurrentDirectory(), configured),
                         Path.Combine(AppContext.BaseDirectory, configured),
                     })
            {
                try
                {
                    var full = Path.GetFullPath(raw);
                    if (Directory.Exists(full) && HasMarker(full, leaf))
                        return full;
                }
                catch
                {
                }
            }
        }

        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(start);
            while (dir != null)
            {
                foreach (var root in new[] { "read_only", "input_data" })
                {
                    var candidate = Path.Combine(dir.FullName, root, leaf);
                    if (Directory.Exists(candidate) && HasMarker(candidate, leaf))
                        return candidate;
                }

                dir = dir.Parent;
            }
        }

        return Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
            ? Path.Combine("read_only", leaf)
            : configured);
    }

    static bool HasMarker(string dir, string leaf)
    {
        if (leaf == "company")
            return File.Exists(Path.Combine(dir, "df_Uid_data_hack.csv"));
        if (leaf == "from_alfa")
            return Directory.EnumerateFiles(dir, "*.txt").Any();
        return true;
    }
}
