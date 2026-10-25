namespace KanaanFlow.Web.Services;

using KanaanFlow.Core.Finance;

/// <summary>How roles are named and colored in the app.</summary>
public static class RoleText
{
    public static string Name(BookService book, TeamRole role) => role switch
    {
        TeamRole.Admin => book.T("roleAdmin"),
        TeamRole.Owner => book.T("roleOwner"),
        TeamRole.Accountant => book.T("roleAccountant"),
        TeamRole.Staff => book.T("roleStaff"),
        _ => book.T("roleViewer")
    };

    public static string Css(TeamRole role) => role switch
    {
        TeamRole.Admin => "admin",
        TeamRole.Owner => "owner",
        TeamRole.Accountant => "accountant",
        TeamRole.Viewer => "viewer",
        _ => string.Empty
    };
}

/// <summary>Turns a server error code into a sentence for the person.</summary>
public static class ErrorText
{
    public static string For(BookService book, string code)
    {
        string key = "err-" + code;
        string text = book.T(key);
        return text == key ? book.T("err-server-error") : text;
    }
}
