namespace Hase.DesktopHost;

public enum DesktopRuntimeByteInterpretationStatus
{
    RecognizedValid = 0,
    RecognizedMalformedOrIncomplete = 1,
    UnsupportedProtocolFamily = 2,
    NoCapturedBytes = 3
}
