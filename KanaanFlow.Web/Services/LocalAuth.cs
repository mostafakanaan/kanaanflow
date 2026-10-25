namespace KanaanFlow.Web.Services;

using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using KanaanFlow.Core.Finance;
using KanaanFlow.Core.Finance.Events;

/// <summary>
/// Log-in for a book kept only on this device, when there is no team server to ask.
/// </summary>
public sealed class LocalAuth
{
    /// <summary>Needed to create a new user. Temporary: it will become configurable.</summary>
    public const string MasterPassword = "WelcomeToKanaanFlow";

    public const int MinPasswordLength = 8;

    private const int Rounds = 20000;

    private readonly BookService book;

    public LocalAuth(BookService book)
    {
        this.book = book;
    }

    /// <summary>Creates a device-only Admin. Returns an error code, or null on success.</summary>
    public async Task<string?> RegisterAsync(string name, string password, string masterPassword)
    {
        if (!FixedEquals(masterPassword, MasterPassword))
        {
            return "wrong-master-password";
        }

        if (name.Length == 0)
        {
            return "invalid-name";
        }

        if (password.Length < MinPasswordLength)
        {
            return "password-too-short";
        }

        if (book.Settings.LocalAccounts.Any(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return "name-taken";
        }

        TeamMember me = new TeamMember { Id = BookService.NewId(), Name = name, Role = TeamRole.Admin };
        string salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        book.Settings.LocalAccounts.Add(new LocalAccount { MemberId = me.Id, Name = name, Salt = salt, Hash = Hash(password, salt) });
        await book.LogInAsync(me.Id, TeamRole.Admin, localOnly: true);
        await book.RecordAsync(new MemberSaved { Member = me });
        return null;
    }

    /// <summary>Logs in a device-only user. Returns an error code, or null on success.</summary>
    public async Task<string?> SignInAsync(string name, string password)
    {
        LocalAccount? account = book.Settings.LocalAccounts.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
        if (account == null)
        {
            return "wrong-name-or-password";
        }

        if (account.Hash.Length == 0)
        {
            // From before passwords: the first password used becomes theirs.
            if (password.Length < MinPasswordLength)
            {
                return "password-too-short";
            }

            account.Salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            account.Hash = Hash(password, account.Salt);
        }
        else if (!FixedEquals(Hash(password, account.Salt), account.Hash))
        {
            return "wrong-name-or-password";
        }

        TeamRole role = book.Book.FindMember(account.MemberId)?.Role ?? TeamRole.Admin;
        await book.LogInAsync(account.MemberId, role, localOnly: true);
        return null;
    }

    /// <summary>Sets a new password for a forgotten one (needs the master password) and logs in. Returns an error code, or null.</summary>
    public async Task<string?> ResetPasswordAsync(string name, string masterPassword, string newPassword)
    {
        if (!FixedEquals(masterPassword, MasterPassword))
        {
            return "wrong-master-password";
        }

        if (newPassword.Length < MinPasswordLength)
        {
            return "password-too-short";
        }

        LocalAccount? account = book.Settings.LocalAccounts.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
        if (account == null)
        {
            return "wrong-name-or-password";
        }

        account.Salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        account.Hash = Hash(newPassword, account.Salt);
        TeamRole role = book.Book.FindMember(account.MemberId)?.Role ?? TeamRole.Admin;
        await book.LogInAsync(account.MemberId, role, localOnly: true);
        return null;
    }

    /// <summary>Changes the logged-in device-only user's password. Returns false when the current one is wrong.</summary>
    public async Task<bool> ChangePasswordAsync(string current, string next)
    {
        LocalAccount? account = book.Settings.LocalAccounts.FirstOrDefault(a => a.MemberId == book.Settings.MemberId);
        if (account == null || (account.Hash.Length > 0 && !FixedEquals(Hash(current, account.Salt), account.Hash)))
        {
            return false;
        }

        account.Salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        account.Hash = Hash(next, account.Salt);
        await book.SaveSettingsAsync();
        return true;
    }

    private static string Hash(string password, string salt)
    {
        byte[] data = SHA256.HashData(Encoding.UTF8.GetBytes(salt + ":" + password));
        for (int i = 0; i < Rounds; i++)
        {
            data = SHA256.HashData(data);
        }

        return Convert.ToBase64String(data);
    }

    private static bool FixedEquals(string a, string b)
    {
        return CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(a)), SHA256.HashData(Encoding.UTF8.GetBytes(b)));
    }
}
