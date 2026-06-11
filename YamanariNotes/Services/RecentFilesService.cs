namespace YamanariNotes.Services;

public sealed class RecentFilesService
{
    private const int MaxItems = 10;

    public List<string> Add(string path, IEnumerable<string> currentItems)
    {
        var files = currentItems
            .Where(item => !string.Equals(item, path, StringComparison.OrdinalIgnoreCase))
            .Prepend(path)
            .Take(MaxItems)
            .ToList();

        return files;
    }
}
