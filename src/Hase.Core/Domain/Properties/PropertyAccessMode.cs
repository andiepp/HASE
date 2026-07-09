namespace Hase.Core.Domain.Properties;

[Flags]
public enum PropertyAccessMode
{
    None = 0,
    Read = 1,
    Write = 2,
    ReadWrite = Read | Write
}