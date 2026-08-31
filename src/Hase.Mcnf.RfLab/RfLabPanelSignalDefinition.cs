using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Instruments;

namespace Hase.Mcnf.RfLab;

/// <summary>
/// Defines the third version of the normalized RF-Lab endpoint: the
/// controlled signal definition, additionally declaring the dedicated
/// operating surface a presentation layer may host for it.
/// </summary>
/// <remarks>
/// The interface is identical to version 2 in every Property and Command. The
/// declaration is additive metadata, so a presentation layer that hosts no
/// panel presents this version exactly as version 2.
/// </remarks>
public static class RfLabPanelSignalDefinition
{
    public static DescriptorReference Reference { get; } =
        new(RfLabReadOnlyDefinition.Reference.Id, version: 3);

    public static EndpointDescriptorDefinition EndpointDefinition { get; } = Create();

    private static EndpointDescriptorDefinition Create()
    {
        InstrumentDescriptor controlled =
            RfLabControlledSignalDefinition.EndpointDefinition.Instruments.Single();

        return new EndpointDescriptorDefinition(
            new EndpointMetadata
            {
                DisplayName = "RF-Lab Signal Laboratory",
                Description =
                    "Controlled RF-Lab signal-generation definition declaring "
                    + "its operating panel."
            },
            [
                controlled with
                {
                    Presentation = new InstrumentPresentation
                    {
                        PanelId = RfLabPanelDeclaration.PanelId
                    }
                }
            ]);
    }
}
