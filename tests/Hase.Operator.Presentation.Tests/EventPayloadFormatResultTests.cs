namespace Hase.Operator.Presentation.Tests;

public sealed class EventPayloadFormatResultTests
{
    [Fact]
    public void Constructor_ShouldPreserveMembers()
    {
        EventPayloadFormatResult result =
            new(
                EventPayloadFormatStatus.Formatted,
                string.Empty);

        Assert.Equal(
            EventPayloadFormatStatus.Formatted,
            result.Status);
        Assert.Equal(
            string.Empty,
            result.Text);
    }

    [Fact]
    public void Constructor_UnknownStatus_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            "status",
            () =>
                new EventPayloadFormatResult(
                    (EventPayloadFormatStatus)999,
                    "Payload"));
    }

    [Fact]
    public void Constructor_NullText_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(
            "text",
            () =>
                new EventPayloadFormatResult(
                    EventPayloadFormatStatus.Formatted,
                    null!));
    }
}
