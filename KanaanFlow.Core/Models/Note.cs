namespace KanaanFlow.Core.Models;

using System;

public sealed class Note
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; }
}
