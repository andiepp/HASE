namespace Hase.Transport.Serial;

/// <summary>
/// Classifies exceptions thrown specifically by an operating-system
/// serial-port open operation.
/// </summary>
internal static class SerialPortOpenFailureClassifier
{
    private const int ErrorFileNotFound =
        2;

    private const int ErrorPathNotFound =
        3;

    private const int ErrorAccessDenied =
        5;

    private const int ErrorSharingViolation =
        32;

    private const int ErrorDeviceNotConnected =
        1167;

    internal static bool TryClassify(
        Exception exception,
        out SerialPortOpenFailure failure)
    {
        ArgumentNullException.ThrowIfNull(
            exception);

        int nativeErrorCode =
            GetNativeErrorCode(
                exception);

        switch (nativeErrorCode)
        {
            case ErrorSharingViolation:
                failure =
                    SerialPortOpenFailure.Busy;

                return true;

            case ErrorFileNotFound:
            case ErrorPathNotFound:
            case ErrorDeviceNotConnected:
                failure =
                    SerialPortOpenFailure.Unavailable;

                return true;

            case ErrorAccessDenied:
                failure =
                    SerialPortOpenFailure.AccessDenied;

                return true;
        }

        switch (exception)
        {
            case UnauthorizedAccessException:
                failure =
                    SerialPortOpenFailure.AccessDenied;

                return true;

            case FileNotFoundException:
            case DirectoryNotFoundException:
                failure =
                    SerialPortOpenFailure.Unavailable;

                return true;

            case IOException:
            case InvalidOperationException:
            case ArgumentException:
                failure =
                    SerialPortOpenFailure.Failed;

                return true;

            default:
                failure =
                    default;

                return false;
        }
    }

    private static int GetNativeErrorCode(
        Exception exception)
    {
        return exception.HResult
            & 0xFFFF;
    }
}