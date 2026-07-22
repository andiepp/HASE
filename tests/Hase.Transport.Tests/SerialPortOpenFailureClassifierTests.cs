using Hase.Transport.Serial;

namespace Hase.Transport.Tests;

public sealed class SerialPortOpenFailureClassifierTests
{
    [Theory]
    [InlineData(32, SerialPortOpenFailure.Busy)]
    [InlineData(2, SerialPortOpenFailure.Unavailable)]
    [InlineData(3, SerialPortOpenFailure.Unavailable)]
    [InlineData(1167, SerialPortOpenFailure.Unavailable)]
    [InlineData(5, SerialPortOpenFailure.AccessDenied)]
    public void TryClassify_KnownWindowsError_ShouldClassify(
        int nativeErrorCode,
        SerialPortOpenFailure expectedFailure)
    {
        // Arrange
        var exception =
            new TestIOException(
                nativeErrorCode);

        // Act
        bool classified =
            SerialPortOpenFailureClassifier.TryClassify(
                exception,
                out SerialPortOpenFailure actualFailure);

        // Assert
        Assert.True(
            classified);

        Assert.Equal(
            expectedFailure,
            actualFailure);
    }

    [Fact]
    public void TryClassify_UnauthorizedAccess_ShouldClassifyAccessDenied()
    {
        // Act
        bool classified =
            SerialPortOpenFailureClassifier.TryClassify(
                new UnauthorizedAccessException(),
                out SerialPortOpenFailure failure);

        // Assert
        Assert.True(
            classified);

        Assert.Equal(
            SerialPortOpenFailure.AccessDenied,
            failure);
    }

    [Fact]
    public void TryClassify_FileNotFound_ShouldClassifyUnavailable()
    {
        // Act
        bool classified =
            SerialPortOpenFailureClassifier.TryClassify(
                new FileNotFoundException(),
                out SerialPortOpenFailure failure);

        // Assert
        Assert.True(
            classified);

        Assert.Equal(
            SerialPortOpenFailure.Unavailable,
            failure);
    }

    [Fact]
    public void TryClassify_DirectoryNotFound_ShouldClassifyUnavailable()
    {
        // Act
        bool classified =
            SerialPortOpenFailureClassifier.TryClassify(
                new DirectoryNotFoundException(),
                out SerialPortOpenFailure failure);

        // Assert
        Assert.True(
            classified);

        Assert.Equal(
            SerialPortOpenFailure.Unavailable,
            failure);
    }

    [Theory]
    [MemberData(nameof(GenericFailures))]
    public void TryClassify_GenericOpenFailure_ShouldClassifyFailed(
        Exception exception)
    {
        // Act
        bool classified =
            SerialPortOpenFailureClassifier.TryClassify(
                exception,
                out SerialPortOpenFailure failure);

        // Assert
        Assert.True(
            classified);

        Assert.Equal(
            SerialPortOpenFailure.Failed,
            failure);
    }

    [Fact]
    public void TryClassify_UnsupportedException_ShouldReturnFalse()
    {
        // Act
        bool classified =
            SerialPortOpenFailureClassifier.TryClassify(
                new NotSupportedException(),
                out _);

        // Assert
        Assert.False(
            classified);
    }

    [Fact]
    public void TryClassify_NullException_ShouldThrow()
    {
        // Act
        void Act()
        {
            _ = SerialPortOpenFailureClassifier.TryClassify(
                null!,
                out _);
        }

        // Assert
        Assert.Throws<ArgumentNullException>(
            Act);
    }

    public static TheoryData<Exception> GenericFailures =>
        new()
        {
            new IOException(),
            new InvalidOperationException(),
            new ArgumentException()
        };

    private sealed class TestIOException
        : IOException
    {
        public TestIOException(
            int nativeErrorCode)
        {
            HResult =
                unchecked(
                    (int)0x80070000)
                | nativeErrorCode;
        }
    }
}