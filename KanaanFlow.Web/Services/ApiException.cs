namespace KanaanFlow.Web.Services;

using System;

/// <summary>
/// A refusal from the sync server, with its error code (for example "wrong-name-or-password").
/// </summary>
public sealed class ApiException : Exception
{
    public ApiException(int status, string code)
        : base(code)
    {
        Status = status;
        Code = code;
    }

    public int Status { get; }

    public string Code { get; }
}
