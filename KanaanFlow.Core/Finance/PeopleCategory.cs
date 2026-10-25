namespace KanaanFlow.Core.Finance;

/// <summary>
/// A group of contacts, such as customers or suppliers.
/// </summary>
public sealed class PeopleCategory
{
    public const string OtherId = "other";

    public string Id { get; set; } = string.Empty;

    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public bool IsLocked => Id == OtherId;

    public string Name(string language) => language == "en" ? NameEn : NameAr;
}
