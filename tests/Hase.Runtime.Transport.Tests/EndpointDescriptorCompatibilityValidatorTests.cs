using Hase.Core.Domain.Data;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class EndpointDescriptorCompatibilityValidatorTests
{
    [Fact]
    public void Validate_NullRuntimeDescriptor_ShouldThrow()
    {
        // Arrange
        var validator =
            new EndpointDescriptorCompatibilityValidator();

        EndpointDescriptor physicalDescriptor =
            CreateDescriptor();

        // Act
        void Act()
        {
            validator.Validate(
                null!,
                physicalDescriptor);
        }

        // Assert
        ArgumentNullException exception =
            Assert.Throws<ArgumentNullException>(
                Act);

        Assert.Equal(
            "runtimeDescriptor",
            exception.ParamName);
    }

    [Fact]
    public void Validate_NullPhysicalDescriptor_ShouldThrow()
    {
        // Arrange
        var validator =
            new EndpointDescriptorCompatibilityValidator();

        EndpointDescriptor runtimeDescriptor =
            CreateDescriptor();

        // Act
        void Act()
        {
            validator.Validate(
                runtimeDescriptor,
                null!);
        }

        // Assert
        ArgumentNullException exception =
            Assert.Throws<ArgumentNullException>(
                Act);

        Assert.Equal(
            "physicalDescriptor",
            exception.ParamName);
    }

    [Fact]
    public void Validate_SameDescriptorInstance_ShouldSucceed()
    {
        // Arrange
        var validator =
            new EndpointDescriptorCompatibilityValidator();

        EndpointDescriptor descriptor =
            CreateDescriptor();

        // Act
        validator.Validate(
            descriptor,
            descriptor);

        // Assert
        Assert.True(
            true);
    }

    [Fact]
    public void Validate_SeparatelyConstructedEquivalentDescriptors_ShouldSucceed()
    {
        // Arrange
        var validator =
            new EndpointDescriptorCompatibilityValidator();

        EndpointDescriptor runtimeDescriptor =
            CreateDescriptor();

        EndpointDescriptor physicalDescriptor =
            CreateDescriptor();

        // Act
        validator.Validate(
            runtimeDescriptor,
            physicalDescriptor);

        // Assert
        Assert.NotSame(
            runtimeDescriptor,
            physicalDescriptor);

        Assert.NotSame(
            runtimeDescriptor.Metadata,
            physicalDescriptor.Metadata);

        Assert.NotSame(
            runtimeDescriptor.Instruments,
            physicalDescriptor.Instruments);
    }

    [Fact]
    public void Validate_DifferentEndpointId_ShouldThrow()
    {
        // Arrange
        var validator =
            new EndpointDescriptorCompatibilityValidator();

        EndpointDescriptor runtimeDescriptor =
            CreateDescriptor(
                endpointId:
                    "endpoint-01");

        EndpointDescriptor physicalDescriptor =
            CreateDescriptor(
                endpointId:
                    "endpoint-02");

        // Act
        void Act()
        {
            validator.Validate(
                runtimeDescriptor,
                physicalDescriptor);
        }

        // Assert
        InvalidDataException exception =
            Assert.Throws<InvalidDataException>(
                Act);

        Assert.Equal(
            "The physical endpoint identifier 'endpoint-02' "
            + "does not match the runtime endpoint identifier "
            + "'endpoint-01'.",
            exception.Message);
    }

    [Fact]
    public void Validate_DifferentEndpointMetadata_ShouldThrow()
    {
        // Arrange
        var validator =
            new EndpointDescriptorCompatibilityValidator();

        EndpointDescriptor runtimeDescriptor =
            CreateDescriptor(
                endpointDisplayName:
                    "Environment Endpoint");

        EndpointDescriptor physicalDescriptor =
            CreateDescriptor(
                endpointDisplayName:
                    "Changed Environment Endpoint");

        // Act
        void Act()
        {
            validator.Validate(
                runtimeDescriptor,
                physicalDescriptor);
        }

        // Assert
        InvalidDataException exception =
            Assert.Throws<InvalidDataException>(
                Act);

        Assert.Equal(
            "The physical descriptor for endpoint 'endpoint-01' "
            + "is not strictly compatible with the existing "
            + "runtime descriptor.",
            exception.Message);
    }

    [Fact]
    public void Validate_DifferentInstrument_ShouldThrow()
    {
        // Arrange
        var validator =
            new EndpointDescriptorCompatibilityValidator();

        EndpointDescriptor runtimeDescriptor =
            CreateDescriptor(
                firstInstrumentName:
                    "Environment Sensor");

        EndpointDescriptor physicalDescriptor =
            CreateDescriptor(
                firstInstrumentName:
                    "Changed Environment Sensor");

        // Act
        void Act()
        {
            validator.Validate(
                runtimeDescriptor,
                physicalDescriptor);
        }

        // Assert
        InvalidDataException exception =
            Assert.Throws<InvalidDataException>(
                Act);

        Assert.Equal(
            "The physical descriptor for endpoint 'endpoint-01' "
            + "is not strictly compatible with the existing "
            + "runtime descriptor.",
            exception.Message);
    }

    [Fact]
    public void Validate_DifferentInstrumentInterface_ShouldThrow()
    {
        // Arrange
        var validator =
            new EndpointDescriptorCompatibilityValidator();

        EndpointDescriptor runtimeDescriptor =
            CreateDescriptor(
                includeProperty:
                    true);

        EndpointDescriptor physicalDescriptor =
            CreateDescriptor(
                includeProperty:
                    false);

        // Act
        void Act()
        {
            validator.Validate(
                runtimeDescriptor,
                physicalDescriptor);
        }

        // Assert
        InvalidDataException exception =
            Assert.Throws<InvalidDataException>(
                Act);

        Assert.Equal(
            "The physical descriptor for endpoint 'endpoint-01' "
            + "is not strictly compatible with the existing "
            + "runtime descriptor.",
            exception.Message);
    }

    [Fact]
    public void Validate_DifferentInstrumentOrdering_ShouldThrow()
    {
        // Arrange
        var validator =
            new EndpointDescriptorCompatibilityValidator();

        EndpointDescriptor runtimeDescriptor =
            CreateDescriptor(
                reverseInstrumentOrder:
                    false);

        EndpointDescriptor physicalDescriptor =
            CreateDescriptor(
                reverseInstrumentOrder:
                    true);

        // Act
        void Act()
        {
            validator.Validate(
                runtimeDescriptor,
                physicalDescriptor);
        }

        // Assert
        InvalidDataException exception =
            Assert.Throws<InvalidDataException>(
                Act);

        Assert.Equal(
            "The physical descriptor for endpoint 'endpoint-01' "
            + "is not strictly compatible with the existing "
            + "runtime descriptor.",
            exception.Message);
    }

    private static EndpointDescriptor CreateDescriptor(
        string endpointId = "endpoint-01",
        string endpointDisplayName = "Environment Endpoint",
        string firstInstrumentName = "Environment Sensor",
        bool includeProperty = true,
        bool reverseInstrumentOrder = false)
    {
        InstrumentDescriptor firstInstrument =
            CreateFirstInstrument(
                firstInstrumentName,
                includeProperty);

        InstrumentDescriptor secondInstrument =
            CreateSecondInstrument();

        InstrumentDescriptor[] instruments =
            reverseInstrumentOrder
                ?
                [
                    secondInstrument,
                    firstInstrument
                ]
                :
                [
                    firstInstrument,
                    secondInstrument
                ];

        return new EndpointDescriptor(
            new EndpointId(
                endpointId),
            instruments)
        {
            Metadata =
                new EndpointMetadata
                {
                    DisplayName =
                        endpointDisplayName,
                    Description =
                        "Test endpoint used for descriptor "
                        + "compatibility validation."
                }
        };
    }

    private static InstrumentDescriptor CreateFirstInstrument(
        string name,
        bool includeProperty)
    {
        IReadOnlyList<PropertyDescriptor> properties =
            includeProperty
                ?
                [
                    CreateTemperatureProperty()
                ]
                :
                [];

        return new InstrumentDescriptor(
            new InstrumentId(
                "environment-sensor-01"),
            name,
            new InstrumentKind(
                "environment-sensor"))
        {
            Interface =
                new InstrumentInterface(
                    properties:
                        properties)
        };
    }

    private static InstrumentDescriptor CreateSecondInstrument()
    {
        return new InstrumentDescriptor(
            new InstrumentId(
                "status-indicator-01"),
            "Status Indicator",
            new InstrumentKind(
                "status-indicator"));
    }

    private static PropertyDescriptor CreateTemperatureProperty()
    {
        return new PropertyDescriptor(
            new PropertyId(
                "environment.temperature"),
            new DescriptorPath(
                "Environment",
                "Temperature"),
            "Temperature",
            new NumericDataDescriptor(
                Quantities.Temperature,
                Units.Celsius));
    }
}