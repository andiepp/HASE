namespace Hase.Transport.Serial;

/// <summary>
/// Defines how a serial transport detects parity errors.
/// </summary>
public enum SerialParity
{
    /// <summary>
    /// No parity bit is transmitted.
    /// </summary>
    None = 0,

    /// <summary>
    /// The parity bit produces an odd number of set bits.
    /// </summary>
    Odd = 1,

    /// <summary>
    /// The parity bit produces an even number of set bits.
    /// </summary>
    Even = 2,

    /// <summary>
    /// The parity bit is always set.
    /// </summary>
    Mark = 3,

    /// <summary>
    /// The parity bit is always cleared.
    /// </summary>
    Space = 4
}