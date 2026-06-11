using System.IO;
using System.Text;

namespace YamanariNotes.Services;

public sealed class FileService
{
    private static readonly Encoding DefaultEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public async Task<string> ReadTextAsync(string path)
    {
        return await File.ReadAllTextAsync(path, Encoding.UTF8);
    }

    public async Task SaveTextAsync(string path, string content)
    {
        await File.WriteAllTextAsync(path, content, DefaultEncoding);
    }
}
