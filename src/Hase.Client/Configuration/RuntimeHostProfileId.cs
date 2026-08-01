using Hase.Core.Domain.Identity;

namespace Hase.Client.Configuration;

/// <summary>
/// Identifies one runtime-host profile within a client installation.
/// </summary>
public sealed record RuntimeHostProfileId
    : HaseId
{
    public const int MaximumLength =
        64;

    /// <summary>
    /// Initializes one stable client-local profile identity.
    /// </summary>
    public RuntimeHostProfileId(
        string value)
        : base(
            value)
    {
        if (Value.Length > MaximumLength)
        {
            throw new ArgumentException(
                $"A runtime-host profile identity must not exceed {MaximumLength} characters.",
                nameof(value));
        }

        if (!IsInitialCharacter(
                Value[0])
            || Value.Skip(1)
                .Any(
                    character =>
                        !IsRemainingCharacter(
                            character)))
        {
            throw new ArgumentException(
                "A runtime-host profile identity must begin with a lowercase "
                + "letter or digit and contain only lowercase letters, "
                + "digits, '.', '_', or '-'.",
                nameof(value));
        }
    }

    private static bool IsInitialCharacter(
        char value) =>
        value is >= 'a' and <= 'z'
        || value is >= '0' and <= '9';

    private static bool IsRemainingCharacter(
        char value) =>
        IsInitialCharacter(
            value)
        || value is '.' or '_' or '-';
}
