using Hase.CompactProtocol;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.DesktopHost.App.Physical;

namespace Hase.DesktopHost.Tests;

public sealed class ArduinoUnoLightCompactDefinitionFactoryTests
{
    private static readonly InstrumentId UvInstrumentId =
        new("arduino-uno-light-uv-01");

    private static readonly InstrumentId SpectralInstrumentId =
        new("arduino-uno-light-spectral-01");

    [Fact]
    public void Create_ShouldReportExactDescriptorReference()
    {
        CompactEndpointDefinition definition =
            ArduinoUnoLightCompactDefinitionFactory.Create();

        Assert.Equal(
            new DescriptorId("arduino-uno-light"),
            definition.DescriptorReference.Id);

        Assert.Equal(
            1,
            definition.DescriptorReference.Version);
    }

    [Fact]
    public void Create_ShouldDeclareBothSensorInstruments()
    {
        CompactEndpointDefinition definition =
            ArduinoUnoLightCompactDefinitionFactory.Create();

        Assert.Equal(
            2,
            definition.DescriptorDefinition.Instruments.Count);

        Assert.Equal(
            [UvInstrumentId, SpectralInstrumentId],
            definition.DescriptorDefinition.Instruments
                .Select(instrument => instrument.Id)
                .ToArray());
    }

    [Theory]
    [InlineData(0x01, "uva-irradiance", "Uv.A")]
    [InlineData(0x02, "uvb-irradiance", "Uv.B")]
    [InlineData(0x03, "uvc-irradiance", "Uv.C")]
    [InlineData(0x04, "uva-alarm-threshold", "Uv.AlarmThreshold")]
    public void Create_UvIrradianceProperties_ShouldUseUnsigned16Encoding(
        byte compactPropertyId,
        string propertyId,
        string path)
    {
        CompactEndpointDefinition definition =
            ArduinoUnoLightCompactDefinitionFactory.Create();

        CompactPropertyMapping mapping =
            FindPropertyMapping(
                definition,
                compactPropertyId);

        Assert.Equal(
            UvInstrumentId,
            mapping.InstrumentId);

        Assert.Equal(
            new PropertyId(propertyId),
            mapping.PropertyId);

        Assert.Equal(
            CompactPropertyValueEncoding.Unsigned16LittleEndian,
            mapping.Encoding);

        PropertyDescriptor descriptor =
            FindPropertyDescriptor(
                definition,
                UvInstrumentId,
                mapping.PropertyId);

        Assert.Equal(
            path,
            descriptor.Path.ToString());

        NumericDataDescriptor numeric =
            Assert.IsType<NumericDataDescriptor>(
                descriptor.Data);

        Assert.Equal(
            Quantities.Irradiance,
            numeric.Quantity);

        Assert.Equal(
            Units.MicrowattPerSquareCentimetre,
            numeric.NativeUnit);

        Assert.NotNull(
            numeric.Range);

        Assert.Equal(
            0.0,
            numeric.Range!.Minimum);

        Assert.Equal(
            ushort.MaxValue,
            numeric.Range.Maximum);
    }

    [Fact]
    public void Create_UvAlarmThreshold_ShouldBeTheOnlyWritableProperty()
    {
        CompactEndpointDefinition definition =
            ArduinoUnoLightCompactDefinitionFactory.Create();

        PropertyDescriptor[] writableProperties =
            definition.DescriptorDefinition.Instruments
                .SelectMany(instrument => instrument.Interface.Properties)
                .Where(property =>
                    property.AccessMode == PropertyAccessMode.ReadWrite)
                .ToArray();

        PropertyDescriptor writable =
            Assert.Single(writableProperties);

        Assert.Equal(
            new PropertyId("uva-alarm-threshold"),
            writable.Id);
    }

    [Fact]
    public void Create_SensorReadyProperties_ShouldUseBooleanEncoding()
    {
        CompactEndpointDefinition definition =
            ArduinoUnoLightCompactDefinitionFactory.Create();

        CompactPropertyMapping uvReady =
            FindPropertyMapping(
                definition,
                0x05);

        CompactPropertyMapping spectralReady =
            FindPropertyMapping(
                definition,
                0x1E);

        Assert.Equal(
            new PropertyId("uv-sensor-ready"),
            uvReady.PropertyId);

        Assert.Equal(
            CompactPropertyValueEncoding.Boolean,
            uvReady.Encoding);

        Assert.Equal(
            UvInstrumentId,
            uvReady.InstrumentId);

        Assert.Equal(
            new PropertyId("spectral-sensor-ready"),
            spectralReady.PropertyId);

        Assert.Equal(
            CompactPropertyValueEncoding.Boolean,
            spectralReady.Encoding);

        Assert.Equal(
            SpectralInstrumentId,
            spectralReady.InstrumentId);
    }

    [Fact]
    public void Create_SpectralChannels_ShouldOccupyContiguousCompactIds()
    {
        CompactEndpointDefinition definition =
            ArduinoUnoLightCompactDefinitionFactory.Create();

        byte[] expectedCompactIds =
            Enumerable
                .Range(0x10, 14)
                .Select(value => (byte)value)
                .ToArray();

        byte[] spectralCompactIds =
            definition.PropertyMappings
                .Where(mapping =>
                    mapping.InstrumentId == SpectralInstrumentId
                    && mapping.Encoding
                        == CompactPropertyValueEncoding.Unsigned16LittleEndian)
                .Select(mapping => mapping.CompactPropertyId)
                .OrderBy(value => value)
                .ToArray();

        Assert.Equal(
            expectedCompactIds,
            spectralCompactIds);
    }

    [Fact]
    public void Create_SpectralChannels_ShouldDeclareCountsInDeclaredOrder()
    {
        CompactEndpointDefinition definition =
            ArduinoUnoLightCompactDefinitionFactory.Create();

        string[] expectedPaths =
        [
            "Spectral.F1",
            "Spectral.F2",
            "Spectral.FZ",
            "Spectral.F3",
            "Spectral.F4",
            "Spectral.F5",
            "Spectral.FY",
            "Spectral.FXL",
            "Spectral.F6",
            "Spectral.F7",
            "Spectral.F8",
            "Spectral.NIR",
            "Spectral.VisibleTopLeft",
            "Spectral.VisibleBottomRight"
        ];

        CompactPropertyMapping[] spectralMappings =
            definition.PropertyMappings
                .Where(mapping =>
                    mapping.InstrumentId == SpectralInstrumentId
                    && mapping.Encoding
                        == CompactPropertyValueEncoding.Unsigned16LittleEndian)
                .OrderBy(mapping => mapping.CompactPropertyId)
                .ToArray();

        string[] actualPaths =
            spectralMappings
                .Select(mapping =>
                    FindPropertyDescriptor(
                            definition,
                            SpectralInstrumentId,
                            mapping.PropertyId)
                        .Path
                        .ToString())
                .ToArray();

        Assert.Equal(
            expectedPaths,
            actualPaths);

        foreach (CompactPropertyMapping mapping in spectralMappings)
        {
            PropertyDescriptor descriptor =
                FindPropertyDescriptor(
                    definition,
                    SpectralInstrumentId,
                    mapping.PropertyId);

            NumericDataDescriptor numeric =
                Assert.IsType<NumericDataDescriptor>(
                    descriptor.Data);

            Assert.Equal(
                Quantities.Count,
                numeric.Quantity);

            Assert.Equal(
                Units.Count,
                numeric.NativeUnit);

            Assert.Equal(
                PropertyAccessMode.Read,
                descriptor.AccessMode);
        }
    }

    [Fact]
    public void Create_ShouldMapBothMeasureCommands()
    {
        CompactEndpointDefinition definition =
            ArduinoUnoLightCompactDefinitionFactory.Create();

        Assert.Equal(
            2,
            definition.CommandMappings.Count);

        CompactCommandMapping measureUv =
            definition.CommandMappings.Single(
                mapping => mapping.CompactCommandId == 0x01);

        CompactCommandMapping measureSpectrum =
            definition.CommandMappings.Single(
                mapping => mapping.CompactCommandId == 0x02);

        Assert.Equal(
            UvInstrumentId,
            measureUv.InstrumentId);

        Assert.Equal(
            "Uv.Measure",
            measureUv.CommandPath.ToString());

        Assert.Equal(
            SpectralInstrumentId,
            measureSpectrum.InstrumentId);

        Assert.Equal(
            "Spectral.Measure",
            measureSpectrum.CommandPath.ToString());
    }

    [Fact]
    public void Create_ShouldMapTheUvAlarmEvent()
    {
        CompactEndpointDefinition definition =
            ArduinoUnoLightCompactDefinitionFactory.Create();

        CompactEventMapping mapping =
            Assert.Single(
                definition.EventMappings);

        Assert.Equal(
            0x01,
            mapping.CompactEventId);

        Assert.Equal(
            UvInstrumentId,
            mapping.InstrumentId);

        Assert.Equal(
            "Uv.AlarmRaised",
            mapping.EventPath.ToString());

        Assert.Equal(
            CompactEventValueEncoding.None,
            mapping.Encoding);
    }

    [Fact]
    public void Create_ShouldNotReuseTheValidationEndpointDescriptor()
    {
        CompactEndpointDefinition light =
            ArduinoUnoLightCompactDefinitionFactory.Create();

        CompactEndpointDefinition validation =
            ArduinoUnoCompactDefinitionFactory.Create();

        Assert.NotEqual(
            validation.DescriptorReference.Id,
            light.DescriptorReference.Id);
    }

    private static CompactPropertyMapping FindPropertyMapping(
        CompactEndpointDefinition definition,
        byte compactPropertyId)
    {
        return definition.PropertyMappings.Single(
            mapping => mapping.CompactPropertyId == compactPropertyId);
    }

    private static PropertyDescriptor FindPropertyDescriptor(
        CompactEndpointDefinition definition,
        InstrumentId instrumentId,
        PropertyId propertyId)
    {
        InstrumentDescriptor instrument =
            definition.DescriptorDefinition.Instruments.Single(
                candidate => candidate.Id == instrumentId);

        return instrument.Interface.Properties.Single(
            property => property.Id == propertyId);
    }
}
