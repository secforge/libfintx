using System.Threading.Tasks;
using System.IO;
using System.Text;
using libfintx.Globals;

namespace libfintx.FinTS;

public class UpdFileStore : IUpdStore
{
    private readonly string _directory;

    public UpdFileStore(string directory)
    {
        _directory = directory;
    }

    private static string MakeFilenameValid(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');
        return value.Replace(" ", "_");
    }

    private string GetFile(int bankCountry, int bankCode, string userId)
        => Path.Combine(_directory, $"{bankCountry}_{bankCode}_{MakeFilenameValid(userId)}.upd");

    public async Task<string?> GetUPD(int bankCountry, int bankCode, string userId)
    {
        var file = GetFile(bankCountry, bankCode, userId);
        if (!File.Exists(file))
            return null;
        return await File.ReadAllTextAsync(file, Encoding.UTF8).ConfigureAwait(false);
    }

    public async Task SaveUPD(int bankCountry, int bankCode, string userId, string upd)
    {
        Directory.CreateDirectory(_directory);
        var file = GetFile(bankCountry, bankCode, userId);
        await File.WriteAllTextAsync(file, upd, Encoding.UTF8).ConfigureAwait(false);
    }

    public Task DeleteUPD(int bankCountry, int bankCode, string userId)
    {
        var file = GetFile(bankCountry, bankCode, userId);
        if (File.Exists(file))
            File.Delete(file);
        return Task.CompletedTask;
    }
}
