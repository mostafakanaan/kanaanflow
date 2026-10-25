namespace KanaanFlow.Core.Finance;

/// <summary>
/// What an entry was for, such as sales or rent.
/// </summary>
public sealed class EntryCategory
{
    public const string OtherId = "other";
    public const string LoanId = "loan";

    public string Id { get; set; } = string.Empty;

    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public CategoryKind Kind { get; set; } = CategoryKind.Both;

    public bool IsLocked => Id == OtherId || Id == LoanId;

    public bool Fits(EntryType type) =>
        Kind == CategoryKind.Both
        || (Kind == CategoryKind.In && type == EntryType.In)
        || (Kind == CategoryKind.Out && type == EntryType.Out);

    public string Name(string language) => language == "en" ? NameEn : NameAr;
}
