namespace KanaanFlow.Tests;

using System;
using KanaanFlow.Core.Finance;
using KanaanFlow.Core.Finance.Events;
using KanaanFlow.Sync.Auth;
using KanaanFlow.Sync.Policy;
using Xunit;

public class SyncServerTests
{
    private const string Me = "me";
    private static readonly Func<string, string?> MineEverywhere = _ => Me;
    private static readonly Func<string, string?> SomeoneElse = _ => "other";

    [Theory]
    [InlineData(TeamRole.Owner)]
    [InlineData(TeamRole.Staff)]
    public void RecordingEntriesContactsAndLoans_IsAllowedForOwnerAndStaff(TeamRole role)
    {
        Assert.Null(ChangePolicy.Check(role, Me, new EntryRecorded(), SomeoneElse, SomeoneElse));
        Assert.Null(ChangePolicy.Check(role, Me, new ContactSaved(), SomeoneElse, SomeoneElse));
        Assert.Null(ChangePolicy.Check(role, Me, new LoanOpened(), SomeoneElse, SomeoneElse));
    }

    [Fact]
    public void Viewer_CannotUploadAnything()
    {
        Assert.Equal("view-only", ChangePolicy.Check(TeamRole.Viewer, Me, new EntryRecorded(), MineEverywhere, MineEverywhere));
    }

    [Fact]
    public void Staff_CanUndoOnlyTheirOwnEntriesAndLoans()
    {
        Assert.Null(ChangePolicy.Check(TeamRole.Staff, Me, new EntryVoided { EntryId = "e" }, MineEverywhere, MineEverywhere));
        Assert.Equal("not-your-entry", ChangePolicy.Check(TeamRole.Staff, Me, new EntryVoided { EntryId = "e" }, SomeoneElse, SomeoneElse));
        Assert.Equal("not-your-entry", ChangePolicy.Check(TeamRole.Staff, Me, new EntryVoided { EntryId = "e" }, _ => null, _ => null));
        Assert.Null(ChangePolicy.Check(TeamRole.Staff, Me, new LoanVoided { LoanId = "l" }, MineEverywhere, MineEverywhere));
        Assert.Equal("not-your-loan", ChangePolicy.Check(TeamRole.Staff, Me, new LoanVoided { LoanId = "l" }, SomeoneElse, SomeoneElse));
    }

    [Fact]
    public void Staff_CannotChangeCategoriesOrOpeningBalances()
    {
        Assert.Equal("owner-only", ChangePolicy.Check(TeamRole.Staff, Me, new EntryCategorySaved(), MineEverywhere, MineEverywhere));
        Assert.Equal("owner-only", ChangePolicy.Check(TeamRole.Staff, Me, new PeopleCategoryRemoved(), MineEverywhere, MineEverywhere));
        Assert.Equal("owner-only", ChangePolicy.Check(TeamRole.Staff, Me, new OpeningBalanceSet(), MineEverywhere, MineEverywhere));
    }

    [Fact]
    public void Owner_CanUndoAnyonesEntries()
    {
        Assert.Null(ChangePolicy.Check(TeamRole.Owner, Me, new EntryVoided { EntryId = "e" }, SomeoneElse, SomeoneElse));
        Assert.Null(ChangePolicy.Check(TeamRole.Owner, Me, new OpeningBalanceSet(), SomeoneElse, SomeoneElse));
    }

    [Theory]
    [InlineData(TeamRole.Owner)]
    [InlineData(TeamRole.Staff)]
    public void TeamChanges_AreOnlyMadeByTheServer(TeamRole role)
    {
        Assert.Equal("members-managed-by-server", ChangePolicy.Check(role, Me, new MemberSaved(), MineEverywhere, MineEverywhere));
        Assert.Equal("members-managed-by-server", ChangePolicy.Check(role, Me, new MemberRemoved(), MineEverywhere, MineEverywhere));
    }

    [Fact]
    public void PasswordHash_VerifiesOnlyTheRightPassword()
    {
        string hash = PasswordHasher.Hash("correct horse");

        Assert.StartsWith("pbkdf2-sha256$", hash);
        Assert.True(PasswordHasher.Verify("correct horse", hash));
        Assert.False(PasswordHasher.Verify("correct horsE", hash));
        Assert.False(PasswordHasher.Verify("correct horse", "garbage"));
        Assert.NotEqual(hash, PasswordHasher.Hash("correct horse"));
    }

    [Fact]
    public void SessionTokens_AreRandomAndStoredOnlyAsHashes()
    {
        (string token, string hash) = PasswordHasher.NewToken();
        (string other, string _) = PasswordHasher.NewToken();

        Assert.NotEqual(token, other);
        Assert.Equal(hash, PasswordHasher.HashToken(token));
        Assert.DoesNotContain(token, hash);
    }
}
