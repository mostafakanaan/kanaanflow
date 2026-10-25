namespace KanaanFlow.Core.Finance.Events;

using System.Text.Json.Serialization;

/// <summary>
/// One change to the book. Changes are only ever appended: an undo is a new change, never a deletion.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(EntryRecorded), "entry.recorded")]
[JsonDerivedType(typeof(EntryVoided), "entry.voided")]
[JsonDerivedType(typeof(EntryApproved), "entry.approved")]
[JsonDerivedType(typeof(EntryDeleted), "entry.deleted")]
[JsonDerivedType(typeof(EntryRestored), "entry.restored")]
[JsonDerivedType(typeof(EntryPurged), "entry.purged")]
[JsonDerivedType(typeof(ContactSaved), "contact.saved")]
[JsonDerivedType(typeof(LoanOpened), "loan.opened")]
[JsonDerivedType(typeof(LoanVoided), "loan.voided")]
[JsonDerivedType(typeof(EntryCategorySaved), "entryCategory.saved")]
[JsonDerivedType(typeof(EntryCategoryRemoved), "entryCategory.removed")]
[JsonDerivedType(typeof(PeopleCategorySaved), "peopleCategory.saved")]
[JsonDerivedType(typeof(PeopleCategoryRemoved), "peopleCategory.removed")]
[JsonDerivedType(typeof(OpeningBalanceSet), "openingBalance.set")]
[JsonDerivedType(typeof(SalarySet), "salary.set")]
[JsonDerivedType(typeof(MemberSaved), "member.saved")]
[JsonDerivedType(typeof(MemberRemoved), "member.removed")]
public abstract class BookChange
{
}

public sealed class EntryRecorded : BookChange
{
    public Entry Entry { get; set; } = new Entry();
}

public sealed class EntryVoided : BookChange
{
    public string EntryId { get; set; } = string.Empty;
}

/// <summary>An Accountant approves an Owner's entry. Informative only: balances count the entry either way.</summary>
public sealed class EntryApproved : BookChange
{
    public string EntryId { get; set; } = string.Empty;
}

/// <summary>An Accountant marks an entry as deleted: it leaves balances and reports but can be restored.</summary>
public sealed class EntryDeleted : BookChange
{
    public string EntryId { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;
}

/// <summary>Brings a deleted entry back.</summary>
public sealed class EntryRestored : BookChange
{
    public string EntryId { get; set; } = string.Empty;
}

/// <summary>
/// An Admin deletes an entry for good. The server removes the entry's other events, and every device removes its copy;
/// only this tombstone (the id alone) stays, so devices that sync later learn to remove theirs.
/// </summary>
public sealed class EntryPurged : BookChange
{
    public string EntryId { get; set; } = string.Empty;
}

/// <summary>Adds a contact, or updates it when the id already exists.</summary>
public sealed class ContactSaved : BookChange
{
    public Contact Contact { get; set; } = new Contact();
}

public sealed class LoanOpened : BookChange
{
    public Loan Loan { get; set; } = new Loan();
}

/// <summary>Removes a loan and undoes every entry tied to it.</summary>
public sealed class LoanVoided : BookChange
{
    public string LoanId { get; set; } = string.Empty;
}

/// <summary>Adds or renames an entry type.</summary>
public sealed class EntryCategorySaved : BookChange
{
    public EntryCategory Category { get; set; } = new EntryCategory();
}

/// <summary>Removes an entry type; its entries move to "Other".</summary>
public sealed class EntryCategoryRemoved : BookChange
{
    public string CategoryId { get; set; } = string.Empty;
}

/// <summary>Adds or renames a people category.</summary>
public sealed class PeopleCategorySaved : BookChange
{
    public PeopleCategory Category { get; set; } = new PeopleCategory();
}

/// <summary>Removes a people category; its contacts move to "Other".</summary>
public sealed class PeopleCategoryRemoved : BookChange
{
    public string CategoryId { get; set; } = string.Empty;
}

public sealed class OpeningBalanceSet : BookChange
{
    public Currency Currency { get; set; }

    public decimal Amount { get; set; }
}

/// <summary>Sets an employee's monthly salary from a month on. An amount of 0 ends it from that month.</summary>
public sealed class SalarySet : BookChange
{
    public SalaryPeriod Salary { get; set; } = new SalaryPeriod();
}

public sealed class MemberSaved : BookChange
{
    public TeamMember Member { get; set; } = new TeamMember();
}

public sealed class MemberRemoved : BookChange
{
    public string MemberId { get; set; } = string.Empty;
}
