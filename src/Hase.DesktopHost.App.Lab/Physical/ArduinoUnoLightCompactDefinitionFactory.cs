using Hase.CompactProtocol;
using Hase.Core.Domain.Commands;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Events;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;

namespace Hase.DesktopHost.App.Lab.Physical;

/// <summary>
/// Creates the host-side compact definition of the Arduino Uno Light
/// endpoint: an Arduino Uno carrying an AS7331 UV sensor and an AS7343
/// 14-channel spectral sensor on its I2C bus.
/// </summary>
internal static class ArduinoUnoLightCompactDefinitionFactory
{
    private const byte UvaIrradianceCompactPropertyId = 0x01;
    private const byte UvbIrradianceCompactPropertyId = 0x02;
    private const byte UvcIrradianceCompactPropertyId = 0x03;
    private const byte UvaAlarmThresholdCompactPropertyId = 0x04;
    private const byte UvSensorReadyCompactPropertyId = 0x05;

    private const byte SpectralF1CompactPropertyId = 0x10;
    private const byte SpectralF2CompactPropertyId = 0x11;
    private const byte SpectralFzCompactPropertyId = 0x12;
    private const byte SpectralF3CompactPropertyId = 0x13;
    private const byte SpectralF4CompactPropertyId = 0x14;
    private const byte SpectralF5CompactPropertyId = 0x15;
    private const byte SpectralFyCompactPropertyId = 0x16;
    private const byte SpectralFxlCompactPropertyId = 0x17;
    private const byte SpectralF6CompactPropertyId = 0x18;
    private const byte SpectralF7CompactPropertyId = 0x19;
    private const byte SpectralF8CompactPropertyId = 0x1A;
    private const byte SpectralNearInfraredCompactPropertyId = 0x1B;
    private const byte SpectralVisibleTopLeftCompactPropertyId = 0x1C;
    private const byte SpectralVisibleBottomRightCompactPropertyId = 0x1D;
    private const byte SpectralSensorReadyCompactPropertyId = 0x1E;

    private const byte MeasureUvCompactCommandId = 0x01;
    private const byte MeasureSpectrumCompactCommandId = 0x02;

    private const byte UvaAlarmRaisedCompactEventId = 0x01;

    private const double MaximumUnsigned16Value = ushort.MaxValue;

    private const string UvIrradianceGroupId = "uv-irradiance";
    private const string SpectralScanGroupId = "spectral-scan";

    private static readonly DescriptorReference DescriptorReference =
        new(
            new DescriptorId("arduino-uno-light"),
            version: 1);

    private static readonly InstrumentId UvInstrumentId =
        new("arduino-uno-light-uv-01");

    private static readonly InstrumentId SpectralInstrumentId =
        new("arduino-uno-light-spectral-01");

    private static readonly PropertyId UvaIrradiancePropertyId =
        new("uva-irradiance");

    private static readonly PropertyId UvbIrradiancePropertyId =
        new("uvb-irradiance");

    private static readonly PropertyId UvcIrradiancePropertyId =
        new("uvc-irradiance");

    private static readonly PropertyId UvaAlarmThresholdPropertyId =
        new("uva-alarm-threshold");

    private static readonly PropertyId UvSensorReadyPropertyId =
        new("uv-sensor-ready");

    private static readonly PropertyId SpectralSensorReadyPropertyId =
        new("spectral-sensor-ready");

    private static readonly DescriptorPath UvaIrradiancePropertyPath =
        new("Uv", "A");

    private static readonly DescriptorPath UvbIrradiancePropertyPath =
        new("Uv", "B");

    private static readonly DescriptorPath UvcIrradiancePropertyPath =
        new("Uv", "C");

    private static readonly DescriptorPath UvaAlarmThresholdPropertyPath =
        new("Uv", "AlarmThreshold");

    private static readonly DescriptorPath UvSensorReadyPropertyPath =
        new("Uv", "SensorReady");

    private static readonly DescriptorPath SpectralSensorReadyPropertyPath =
        new("Spectral", "SensorReady");

    private static readonly DescriptorPath MeasureUvCommandPath =
        new("Uv", "Measure");

    private static readonly DescriptorPath MeasureSpectrumCommandPath =
        new("Spectral", "Measure");

    private static readonly DescriptorPath UvaAlarmRaisedEventPath =
        new("Uv", "AlarmRaised");

    private static readonly SpectralChannel[] SpectralChannels =
    [
        new(
            SpectralF1CompactPropertyId,
            "spectral-f1",
            "F1",
            "F1 405 nm",
            405.0),
        new(
            SpectralF2CompactPropertyId,
            "spectral-f2",
            "F2",
            "F2 425 nm",
            425.0),
        new(
            SpectralFzCompactPropertyId,
            "spectral-fz",
            "FZ",
            "FZ 450 nm",
            450.0),
        new(
            SpectralF3CompactPropertyId,
            "spectral-f3",
            "F3",
            "F3 475 nm",
            475.0),
        new(
            SpectralF4CompactPropertyId,
            "spectral-f4",
            "F4",
            "F4 515 nm",
            515.0),
        new(
            SpectralF5CompactPropertyId,
            "spectral-f5",
            "F5",
            "F5 550 nm",
            550.0),
        new(
            SpectralFyCompactPropertyId,
            "spectral-fy",
            "FY",
            "FY 555 nm",
            555.0),
        new(
            SpectralFxlCompactPropertyId,
            "spectral-fxl",
            "FXL",
            "FXL 600 nm",
            600.0),
        new(
            SpectralF6CompactPropertyId,
            "spectral-f6",
            "F6",
            "F6 640 nm",
            640.0),
        new(
            SpectralF7CompactPropertyId,
            "spectral-f7",
            "F7",
            "F7 690 nm",
            690.0),
        new(
            SpectralF8CompactPropertyId,
            "spectral-f8",
            "F8",
            "F8 745 nm",
            745.0),
        new(
            SpectralNearInfraredCompactPropertyId,
            "spectral-nir",
            "NIR",
            "NIR 855 nm",
            855.0),
        new(
            SpectralVisibleTopLeftCompactPropertyId,
            "spectral-visible-top-left",
            "VisibleTopLeft",
            "Visible Top Left"),
        new(
            SpectralVisibleBottomRightCompactPropertyId,
            "spectral-visible-bottom-right",
            "VisibleBottomRight",
            "Visible Bottom Right")
    ];

    public static CompactEndpointDefinition Create()
    {
        EndpointDescriptorDefinition descriptorDefinition =
            CreateDescriptorDefinition();

        var propertyMappings =
            new List<CompactPropertyMapping>
            {
                new(
                    UvaIrradianceCompactPropertyId,
                    UvInstrumentId,
                    UvaIrradiancePropertyId,
                    CompactPropertyValueEncoding.Unsigned16LittleEndian),
                new(
                    UvbIrradianceCompactPropertyId,
                    UvInstrumentId,
                    UvbIrradiancePropertyId,
                    CompactPropertyValueEncoding.Unsigned16LittleEndian),
                new(
                    UvcIrradianceCompactPropertyId,
                    UvInstrumentId,
                    UvcIrradiancePropertyId,
                    CompactPropertyValueEncoding.Unsigned16LittleEndian),
                new(
                    UvaAlarmThresholdCompactPropertyId,
                    UvInstrumentId,
                    UvaAlarmThresholdPropertyId,
                    CompactPropertyValueEncoding.Unsigned16LittleEndian),
                new(
                    UvSensorReadyCompactPropertyId,
                    UvInstrumentId,
                    UvSensorReadyPropertyId,
                    CompactPropertyValueEncoding.Boolean)
            };

        foreach (SpectralChannel channel in SpectralChannels)
        {
            propertyMappings.Add(
                new CompactPropertyMapping(
                    channel.CompactPropertyId,
                    SpectralInstrumentId,
                    new PropertyId(
                        channel.PropertyId),
                    CompactPropertyValueEncoding.Unsigned16LittleEndian));
        }

        propertyMappings.Add(
            new CompactPropertyMapping(
                SpectralSensorReadyCompactPropertyId,
                SpectralInstrumentId,
                SpectralSensorReadyPropertyId,
                CompactPropertyValueEncoding.Boolean));

        return new CompactEndpointDefinition(
            DescriptorReference,
            descriptorDefinition,
            propertyMappings,
            [
                new CompactEventMapping(
                    UvaAlarmRaisedCompactEventId,
                    UvInstrumentId,
                    UvaAlarmRaisedEventPath,
                    CompactEventValueEncoding.None)
            ],
            [
                new CompactCommandMapping(
                    MeasureUvCompactCommandId,
                    UvInstrumentId,
                    MeasureUvCommandPath),
                new CompactCommandMapping(
                    MeasureSpectrumCompactCommandId,
                    SpectralInstrumentId,
                    MeasureSpectrumCommandPath)
            ]);
    }

    private static EndpointDescriptorDefinition CreateDescriptorDefinition()
    {
        PropertyDescriptor uvaIrradiance =
            CreateIrradianceProperty(
                UvaIrradiancePropertyId,
                UvaIrradiancePropertyPath,
                "UV-A Irradiance",
                "Reports the UV-A irradiance measured by the AS7331 sensor.",
                PropertyAccessMode.Read,
                UvIrradianceGroupId);

        PropertyDescriptor uvbIrradiance =
            CreateIrradianceProperty(
                UvbIrradiancePropertyId,
                UvbIrradiancePropertyPath,
                "UV-B Irradiance",
                "Reports the UV-B irradiance measured by the AS7331 sensor.",
                PropertyAccessMode.Read,
                UvIrradianceGroupId);

        PropertyDescriptor uvcIrradiance =
            CreateIrradianceProperty(
                UvcIrradiancePropertyId,
                UvcIrradiancePropertyPath,
                "UV-C Irradiance",
                "Reports the UV-C irradiance measured by the AS7331 sensor.",
                PropertyAccessMode.Read,
                UvIrradianceGroupId);

        PropertyDescriptor uvaAlarmThreshold =
            CreateIrradianceProperty(
                UvaAlarmThresholdPropertyId,
                UvaAlarmThresholdPropertyPath,
                "UV-A Alarm Threshold",
                "Controls the UV-A irradiance above which the endpoint "
                + "publishes the UV-A alarm Event. Zero disables the alarm.",
                PropertyAccessMode.ReadWrite);

        var uvSensorReady =
            new PropertyDescriptor(
                UvSensorReadyPropertyId,
                UvSensorReadyPropertyPath,
                "UV Sensor Ready",
                new BooleanDataDescriptor())
            {
                Description =
                    "Reports whether the endpoint initialized the AS7331 "
                    + "sensor on its I2C bus.",
                AccessMode =
                    PropertyAccessMode.Read
            };

        var measureUv =
            new CommandDescriptor(
                MeasureUvCommandPath,
                "Measure UV")
            {
                Description =
                    "Refreshes the cached AS7331 UV readings from one "
                    + "immediate acquisition."
            };

        var uvaAlarmRaised =
            new EventDescriptor(
                UvaAlarmRaisedEventPath,
                "UV-A Alarm Raised")
            {
                Description =
                    "Raised once each time the measured UV-A irradiance "
                    + "rises above the configured alarm threshold."
            };

        var uvSensor =
            new InstrumentDescriptor(
                UvInstrumentId,
                "AS7331 UV Sensor",
                new InstrumentKind("sensor"))
            {
                Metadata =
                    new InstrumentMetadata
                    {
                        Manufacturer = "ams-OSRAM",
                        Model = "AS7331",
                        Description =
                            "UV sensor on the Arduino Uno I2C bus. It "
                            + "exposes UV-A, UV-B, and UV-C irradiance as "
                            + "read-only Properties, a writable UV-A alarm "
                            + "threshold, an immediate measurement Command, "
                            + "and an alarm Event."
                    },
                Interface =
                    new InstrumentInterface(
                        properties:
                        [
                            uvaIrradiance,
                            uvbIrradiance,
                            uvcIrradiance,
                            uvaAlarmThreshold,
                            uvSensorReady
                        ],
                        commands: [measureUv],
                        events: [uvaAlarmRaised])
            };

        var spectralProperties =
            new List<PropertyDescriptor>();

        foreach (SpectralChannel channel in SpectralChannels)
        {
            spectralProperties.Add(
                CreateSpectralChannelProperty(
                    channel));
        }

        spectralProperties.Add(
            new PropertyDescriptor(
                SpectralSensorReadyPropertyId,
                SpectralSensorReadyPropertyPath,
                "Spectral Sensor Ready",
                new BooleanDataDescriptor())
            {
                Description =
                    "Reports whether the endpoint initialized the AS7343 "
                    + "sensor on its I2C bus.",
                AccessMode =
                    PropertyAccessMode.Read
            });

        var measureSpectrum =
            new CommandDescriptor(
                MeasureSpectrumCommandPath,
                "Measure Spectrum")
            {
                Description =
                    "Refreshes the cached AS7343 channel readings from one "
                    + "immediate acquisition."
            };

        var spectralSensor =
            new InstrumentDescriptor(
                SpectralInstrumentId,
                "AS7343 Spectral Sensor",
                new InstrumentKind("sensor"))
            {
                Metadata =
                    new InstrumentMetadata
                    {
                        Manufacturer = "ams-OSRAM",
                        Model = "AS7343",
                        Description =
                            "14-channel spectral sensor on the Arduino Uno "
                            + "I2C bus. Each channel is exposed as a "
                            + "read-only Property reporting the raw "
                            + "acquisition counts of one photodiode."
                    },
                Interface =
                    new InstrumentInterface(
                        properties: spectralProperties,
                        commands: [measureSpectrum],
                        events: [])
            };

        return new EndpointDescriptorDefinition(
            new EndpointMetadata
            {
                DisplayName =
                    "Arduino Uno Light Endpoint",
                Description =
                    "Physical Arduino Uno-class endpoint exposing an AS7331 "
                    + "UV sensor and an AS7343 14-channel spectral sensor "
                    + "over Compact Serial Protocol Version 1."
            },
            instruments:
            [
                uvSensor,
                spectralSensor
            ]);
    }

    private static PropertyDescriptor CreateIrradianceProperty(
        PropertyId propertyId,
        DescriptorPath propertyPath,
        string displayName,
        string description,
        PropertyAccessMode accessMode,
        string? groupId = null)
    {
        return new PropertyDescriptor(
            propertyId,
            propertyPath,
            displayName,
            new NumericDataDescriptor(
                Quantities.Irradiance,
                Units.MicrowattPerSquareCentimetre,
                new ValueRange(
                    0.0,
                    MaximumUnsigned16Value),
                new Resolution(
                    1.0)))
        {
            Description =
                description,
            AccessMode =
                accessMode,
            Presentation =
                groupId is null
                    ? null
                    : new PropertyPresentation
                    {
                        GroupId = groupId
                    }
        };
    }

    private static PropertyDescriptor CreateSpectralChannelProperty(
        SpectralChannel channel)
    {
        return new PropertyDescriptor(
            new PropertyId(
                channel.PropertyId),
            new DescriptorPath(
                "Spectral",
                channel.PathSegment),
            channel.DisplayName,
            new NumericDataDescriptor(
                Quantities.Count,
                Units.Count,
                new ValueRange(
                    0.0,
                    MaximumUnsigned16Value),
                new Resolution(
                    1.0)))
        {
            Description =
                "Reports the raw AS7343 acquisition counts of channel "
                + channel.DisplayName
                + ".",
            AccessMode =
                PropertyAccessMode.Read,
            Presentation =
                channel.WavelengthNanometres is null
                    ? null
                    : new PropertyPresentation
                    {
                        GroupId = SpectralScanGroupId,
                        Abscissa =
                            new QuantityValue(
                                channel.WavelengthNanometres.Value,
                                Units.Nanometre)
                    }
        };
    }

    private sealed record SpectralChannel(
        byte CompactPropertyId,
        string PropertyId,
        string PathSegment,
        string DisplayName,
        double? WavelengthNanometres = null);
}
