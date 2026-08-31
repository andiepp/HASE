using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;

namespace Hase.Core.Tests;

public sealed class InstrumentPresentationTests
{
    [Fact]
    public void PanelId_IsOptionalAndAbsentByDefault()
    {
        var presentation = new InstrumentPresentation();

        Assert.Null(presentation.PanelId);
    }

    [Theory]
    [InlineData("rf-lab-signal-lab")]
    [InlineData("panel.v2")]
    [InlineData("A1")]
    public void PanelId_AcceptsBoundedIdentifierTokens(string panelId)
    {
        var presentation = new InstrumentPresentation { PanelId = panelId };

        Assert.Equal(panelId, presentation.PanelId);
    }

    [Fact]
    public void PanelId_IsTrimmed()
    {
        var presentation = new InstrumentPresentation
        {
            PanelId = "  rf-lab-signal-lab  "
        };

        Assert.Equal("rf-lab-signal-lab", presentation.PanelId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void PanelId_RejectsEmptyDeclarations(string panelId)
    {
        Assert.Throws<ArgumentException>(
            () => new InstrumentPresentation { PanelId = panelId });
    }

    [Theory]
    [InlineData("panel id")]
    [InlineData("panel/id")]
    [InlineData("panel:id")]
    [InlineData("panel\nid")]
    [InlineData("pänel")]
    public void PanelId_RejectsCharactersOutsideTheIdentifierToken(string panelId)
    {
        Assert.Throws<ArgumentException>(
            () => new InstrumentPresentation { PanelId = panelId });
    }

    [Fact]
    public void PanelId_RejectsUnboundedText()
    {
        string tooLong = new('a', InstrumentPresentation.MaximumPanelIdLength + 1);

        Assert.Throws<ArgumentException>(
            () => new InstrumentPresentation { PanelId = tooLong });

        var accepted = new InstrumentPresentation
        {
            PanelId = new string('a', InstrumentPresentation.MaximumPanelIdLength)
        };
        Assert.Equal(
            InstrumentPresentation.MaximumPanelIdLength,
            accepted.PanelId!.Length);
    }

    [Fact]
    public void InstrumentDescriptor_CarriesNoPresentationByDefault()
    {
        var descriptor = new InstrumentDescriptor(
            new InstrumentId("instrument-01"),
            "Instrument",
            new InstrumentKind("Generic"));

        Assert.Null(descriptor.Presentation);
    }

    [Fact]
    public void InstrumentDescriptor_PreservesDeclaredPresentation()
    {
        var presentation = new InstrumentPresentation
        {
            PanelId = "rf-lab-signal-lab"
        };

        var descriptor = new InstrumentDescriptor(
            new InstrumentId("instrument-01"),
            "Instrument",
            new InstrumentKind("Generic"))
        {
            Presentation = presentation
        };

        Assert.Same(presentation, descriptor.Presentation);
    }
}
