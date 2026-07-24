using Hase.Core.Domain.Data;
using Hase.Core.Domain.Identity;
using Hase.Core.Domain.Properties;
using Hase.Runtime.Northbound;

namespace Hase.Runtime.Tests.Northbound;

public sealed class RuntimeHostPropertyValueValidatorTests
{
    [Fact]
    public void IsValid_NullDescriptor_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => RuntimeHostPropertyValueValidator.IsValid(
                null!,
                "value"));
    }

    [Fact]
    public void IsValid_BooleanDescriptor_AcceptsOnlyBoolean()
    {
        PropertyDescriptor descriptor =
            CreateDescriptor(
                new BooleanDataDescriptor());

        Assert.True(
            RuntimeHostPropertyValueValidator.IsValid(
                descriptor,
                true));

        Assert.False(
            RuntimeHostPropertyValueValidator.IsValid(
                descriptor,
                1));

        Assert.False(
            RuntimeHostPropertyValueValidator.IsValid(
                descriptor,
                null));
    }

    [Fact]
    public void IsValid_StringDescriptor_AcceptsOnlyString()
    {
        PropertyDescriptor descriptor =
            CreateDescriptor(
                new StringDataDescriptor());

        Assert.True(
            RuntimeHostPropertyValueValidator.IsValid(
                descriptor,
                string.Empty));

        Assert.False(
            RuntimeHostPropertyValueValidator.IsValid(
                descriptor,
                123));

        Assert.False(
            RuntimeHostPropertyValueValidator.IsValid(
                descriptor,
                null));
    }

    [Theory]
    [InlineData((byte)1)]
    [InlineData(-2)]
    [InlineData(3L)]
    [InlineData(4.5)]
    public void IsValid_NumericDescriptor_AcceptsClrNumericValues(
        object requestedValue)
    {
        PropertyDescriptor descriptor =
            CreateDescriptor(
                CreateNumericDescriptor());

        Assert.True(
            RuntimeHostPropertyValueValidator.IsValid(
                descriptor,
                requestedValue));
    }

    [Fact]
    public void IsValid_NumericDescriptor_RejectsNonnumericValue()
    {
        PropertyDescriptor descriptor =
            CreateDescriptor(
                CreateNumericDescriptor());

        Assert.False(
            RuntimeHostPropertyValueValidator.IsValid(
                descriptor,
                "1.0"));

        Assert.False(
            RuntimeHostPropertyValueValidator.IsValid(
                descriptor,
                true));

        Assert.False(
            RuntimeHostPropertyValueValidator.IsValid(
                descriptor,
                null));
    }

    [Theory]
    [InlineData(-10.0, true)]
    [InlineData(0.0, true)]
    [InlineData(10.0, true)]
    [InlineData(-10.1, false)]
    [InlineData(10.1, false)]
    public void IsValid_NumericRange_EnforcesInclusiveBounds(
        double requestedValue,
        bool expected)
    {
        PropertyDescriptor descriptor =
            CreateDescriptor(
                new NumericDataDescriptor(
                    Quantities.Temperature,
                    Units.Celsius,
                    new ValueRange(
                        -10.0,
                        10.0)));

        Assert.Equal(
            expected,
            RuntimeHostPropertyValueValidator.IsValid(
                descriptor,
                requestedValue));
    }

    [Fact]
    public void IsValid_UnknownDescriptor_DefersToEndpoint()
    {
        PropertyDescriptor descriptor =
            CreateDescriptor(
                new TestDataDescriptor());

        Assert.True(
            RuntimeHostPropertyValueValidator.IsValid(
                descriptor,
                null));

        Assert.True(
            RuntimeHostPropertyValueValidator.IsValid(
                descriptor,
                new object()));
    }

    private static PropertyDescriptor CreateDescriptor(
        DataDescriptor dataDescriptor)
    {
        return new PropertyDescriptor(
            new PropertyId(
                "property-one"),
            new DescriptorPath(
                "Instrument",
                "Property"),
            "Property",
            dataDescriptor);
    }

    private static NumericDataDescriptor CreateNumericDescriptor()
    {
        return new NumericDataDescriptor(
            Quantities.Temperature,
            Units.Celsius);
    }

    private sealed record TestDataDescriptor
        : DataDescriptor;
}