using System.Threading.Tasks;
namespace libfintx.FinTS;

/// <summary>
/// A generic interface for storing user parameter data (UPD).
/// </summary>
public interface IUpdStore
{
    Task<string?> GetUPD(int bankCountry, int bankCode, string userId);
    Task SaveUPD(int bankCountry, int bankCode, string userId, string upd);
    Task DeleteUPD(int bankCountry, int bankCode, string userId);
}
