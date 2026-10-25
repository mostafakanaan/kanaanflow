namespace KanaanFlow.Sync.Policy;

using System;
using KanaanFlow.Core.Finance;
using KanaanFlow.Core.Finance.Events;

/// <summary>
/// Decides whether a team member may upload a change. The server enforces this, so roles hold even if an app is tampered with.
/// </summary>
public static class ChangePolicy
{
    /// <summary>Returns why the change is refused, or null when it is allowed.</summary>
    /// <param name="entryOwner">Looks up who uploaded an entry (null when unknown).</param>
    /// <param name="loanOwner">Looks up who uploaded a loan (null when unknown).</param>
    public static string? Check(
        TeamRole role,
        string memberId,
        BookChange change,
        Func<string, string?> entryOwner,
        Func<string, string?> loanOwner)
    {
        if (!RoleRules.CanRecord(role))
        {
            return "view-only";
        }

        if (change is MemberSaved || change is MemberRemoved)
        {
            return "members-managed-by-server";
        }

        if (change is EntryApproved)
        {
            return RoleRules.CanApprove(role) ? null : "accountant-only";
        }

        if (RoleRules.CanManageBook(role))
        {
            return null;
        }

        switch (change)
        {
            case EntryRecorded:
            case ContactSaved:
            case LoanOpened:
                return null;

            case EntryVoided voided:
                return entryOwner(voided.EntryId) == memberId ? null : "not-your-entry";

            case LoanVoided loanVoided:
                return loanOwner(loanVoided.LoanId) == memberId ? null : "not-your-loan";

            default:
                return "owner-only";
        }
    }
}
