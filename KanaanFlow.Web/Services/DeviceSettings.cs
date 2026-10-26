namespace KanaanFlow.Web.Services;

using System;
using System.Collections.Generic;
using KanaanFlow.Core.Finance;

/// <summary>
/// Settings that belong to this device only and never sync.
/// </summary>
public sealed class DeviceSettings
{
    /// <summary>Bumped when stored settings need upgrading on load.</summary>
    public int Version { get; set; }

    public string Language { get; set; } = "ar";

    public double Zoom { get; set; } = 1.0;

    public string DeviceId { get; set; } = string.Empty;

    /// <summary>The team member logged in on this device, empty when logged out.</summary>
    public string MemberId { get; set; } = string.Empty;

    /// <summary>The logged-in member's role as the server last said, used until the team list has synced.</summary>
    public TeamRole Role { get; set; } = TeamRole.Viewer;

    /// <summary>True while someone is logged in on this device.</summary>
    public bool SetupDone { get; set; }

    /// <summary>A book kept only on this device (no sync server): users sign in against <see cref="LocalAccounts"/>.</summary>
    public bool LocalOnly { get; set; }

    public List<LocalAccount> LocalAccounts { get; set; } = new List<LocalAccount>();

    public string LastCurrency { get; set; } = "USD";

    /// <summary>The sync server session, empty when this device isn't signed in.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Highest server sequence number already pulled.</summary>
    public long PulledSeq { get; set; }

    /// <summary>The app build that last pulled. A new build pulls everything once, to read changes an older one skipped.</summary>
    public string PulledWithBuild { get; set; } = string.Empty;

    public DateTime? LastSyncUtc { get; set; }

    /// <summary>The version of the team book this device holds; a different one on the server means an Admin reset it.</summary>
    public string BookEpoch { get; set; } = string.Empty;

    /// <summary>A message for the person that must not be missed (such as "the book was reset"), shown until dismissed.</summary>
    public string Notice { get; set; } = string.Empty;

    public bool SignedIn => !string.IsNullOrEmpty(Token);
}

/// <summary>A user of a device-only book. The password is stored only as a salted hash.</summary>
public sealed class LocalAccount
{
    public string MemberId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Salt { get; set; } = string.Empty;

    /// <summary>Empty for users from before passwords: the first password they sign in with becomes theirs.</summary>
    public string Hash { get; set; } = string.Empty;
}
