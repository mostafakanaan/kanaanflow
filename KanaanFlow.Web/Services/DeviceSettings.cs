namespace KanaanFlow.Web.Services;

using System;

/// <summary>
/// Settings that belong to this device only and never sync.
/// </summary>
public sealed class DeviceSettings
{
    public string Language { get; set; } = "ar";

    public double Zoom { get; set; } = 1.0;

    public string DeviceId { get; set; } = string.Empty;

    /// <summary>The team member who uses this device.</summary>
    public string MemberId { get; set; } = string.Empty;

    public bool SetupDone { get; set; }

    public string LastCurrency { get; set; } = "USD";

    /// <summary>The sync server session, empty when this device isn't signed in.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Highest server sequence number already pulled.</summary>
    public long PulledSeq { get; set; }

    public DateTime? LastSyncUtc { get; set; }

    public bool SignedIn => !string.IsNullOrEmpty(Token);
}
