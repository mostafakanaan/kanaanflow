namespace KanaanFlow.Sync.Data;

using System;

/// <summary>
/// A device sent changes for an older version of the book: an Admin has reset it since.
/// </summary>
public sealed class BookResetException : Exception
{
    public BookResetException(string epoch)
        : base("The book was reset.")
    {
        Epoch = epoch;
    }

    /// <summary>The book's current version.</summary>
    public string Epoch { get; }
}
