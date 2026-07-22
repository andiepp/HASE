using Hase.CompactProtocol;
using Hase.Core.Domain.Data;
using Hase.Core.Domain.Descriptors;
using Hase.Core.Domain.Endpoints;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Instruments;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Runtime;
using Xunit;

namespace Hase.Runtime.Transport.Tests;

public sealed class CompactRuntimePropertyWriteResultTests
{
    private static readonly InstrumentId InstrumentId =
        new(
            "controller-01");

    private static readonly PropertyId PropertyId =
        new(
            "led-state");

    [Fact]
    public void Constructor_SuccessfulWriteAndRead_ShouldReportCacheUpdated()
    {
        CompactPropertyMapping mapping =
            CreateMapping();

        RuntimeProperty runtimeProperty =
            CreateRuntimeProperty();

        var result =
            new CompactRuntimePropertyWriteResult(
                mapping,
                runtimeProperty,
                CompactPropertyWriteStatus.Success,
                CompactPropertyReadStatus.Success);

        Assert.Same(
            mapping,
            result.Mapping);

        Assert.Same(
            runtimeProperty,
            result.RuntimeProperty);

        Assert.Equal(
            CompactPropertyWriteStatus.Success,
            result.WriteStatus);

        Assert.Equal(
            CompactPropertyReadStatus.Success,
            result.ConfirmationReadStatus);

        Assert.True(
            result.CacheUpdated);
    }

    [Theory]
    [InlineData(
        0x01)]
    [InlineData(
        0x02)]
    public void Constructor_SuccessfulWriteAndFailedRead_ShouldRetainCache(
        byte readStatusByte)
    {
        CompactPropertyReadStatus readStatus =
            (CompactPropertyReadStatus)readStatusByte;

        var result =
            new CompactRuntimePropertyWriteResult(
                CreateMapping(),
                CreateRuntimeProperty(),
                CompactPropertyWriteStatus.Success,
                readStatus);

        Assert.Equal(
            readStatus,
            result.ConfirmationReadStatus);

        Assert.False(
            result.CacheUpdated);
    }

    [Theory]
    [InlineData(
        0x01)]
    [InlineData(
        0x02)]
    [InlineData(
        0x03)]
    [InlineData(
        0x04)]
    public void Constructor_UnsuccessfulWrite_ShouldNotRequireRead(
        byte writeStatusByte)
    {
        CompactPropertyWriteStatus writeStatus =
            (CompactPropertyWriteStatus)writeStatusByte;

        var result =
            new CompactRuntimePropertyWriteResult(
                CreateMapping(),
                CreateRuntimeProperty(),
                writeStatus,
                confirmationReadStatus: null);

        Assert.Equal(
            writeStatus,
            result.WriteStatus);

        Assert.Null(
            result.ConfirmationReadStatus);

        Assert.False(
            result.CacheUpdated);
    }

    [Fact]
    public void Constructor_SuccessWithoutConfirmationRead_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactRuntimePropertyWriteResult(
                CreateMapping(),
                CreateRuntimeProperty(),
                CompactPropertyWriteStatus.Success,
                confirmationReadStatus: null);
        }

        Assert.Throws<ArgumentException>(
            Act);
    }

    [Fact]
    public void Constructor_FailedWriteWithConfirmationRead_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactRuntimePropertyWriteResult(
                CreateMapping(),
                CreateRuntimeProperty(),
                CompactPropertyWriteStatus.WriteFailed,
                CompactPropertyReadStatus.Success);
        }

        Assert.Throws<ArgumentException>(
            Act);
    }

    [Fact]
    public void Constructor_UndefinedWriteStatus_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactRuntimePropertyWriteResult(
                CreateMapping(),
                CreateRuntimeProperty(),
                (CompactPropertyWriteStatus)0xFF,
                confirmationReadStatus: null);
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            Act);
    }

    [Fact]
    public void Constructor_UndefinedReadStatus_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactRuntimePropertyWriteResult(
                CreateMapping(),
                CreateRuntimeProperty(),
                CompactPropertyWriteStatus.Success,
                (CompactPropertyReadStatus)0xFF);
        }

        Assert.Throws<ArgumentOutOfRangeException>(
            Act);
    }

    [Fact]
    public void Constructor_NullMapping_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactRuntimePropertyWriteResult(
                null!,
                CreateRuntimeProperty(),
                CompactPropertyWriteStatus.WriteFailed,
                confirmationReadStatus: null);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    [Fact]
    public void Constructor_NullRuntimeProperty_ShouldThrow()
    {
        void Act()
        {
            _ = new CompactRuntimePropertyWriteResult(
                CreateMapping(),
                null!,
                CompactPropertyWriteStatus.WriteFailed,
                confirmationReadStatus: null);
        }

        Assert.Throws<ArgumentNullException>(
            Act);
    }

    private static CompactPropertyMapping CreateMapping()
    {
        return new CompactPropertyMapping(
            compactPropertyId: 0x01,
            InstrumentId,
            PropertyId,
            CompactPropertyValueEncoding.Boolean);
    }

    private static RuntimeProperty CreateRuntimeProperty()
    {
        var property =
            new PropertyDescriptor(
                PropertyId,
                new DescriptorPath(
                    "Led",
                    "State"),
                "Built-in LED State",
                new BooleanDataDescriptor())
            {
                AccessMode =
                    PropertyAccessMode.ReadWrite
            };

        var instrument =
            new InstrumentDescriptor(
                InstrumentId,
                "Arduino Uno GPIO Controller",
                new InstrumentKind(
                    "controller"))
            {
                Interface =
                    new InstrumentInterface(
                        properties:
                        [
                            property
                        ])
            };

        var definition =
            new EndpointDescriptorDefinition(
                metadata:
                    new(),
                instruments:
                [
                    instrument
                ]);

        RuntimeEndpoint runtimeEndpoint =
            new RuntimeContext()
                .AddEndpoint(
                    definition.Materialize(
                        new EndpointId(
                            "arduino-uno-01")));

        return runtimeEndpoint
            .FindInstrument(
                InstrumentId)!
            .FindProperty(
                PropertyId)!;
    }
}