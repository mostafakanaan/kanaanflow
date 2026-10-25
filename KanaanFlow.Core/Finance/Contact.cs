namespace KanaanFlow.Core.Finance;

/// <summary>
/// A person or company money is received from or paid to.
/// </summary>
public sealed class Contact
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string CategoryId { get; set; } = PeopleCategory.OtherId;

    public string Phone { get; set; } = string.Empty;

    public Contact Clone() => (Contact)MemberwiseClone();
}
