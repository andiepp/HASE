using Hase.Transport.Discovery;

namespace Hase.Transport.Tests;

public sealed class WindowsUsbSerialDeviceRecordParserTests
{
    [Fact]
    public void Parse_CompleteUsbRecord_ShouldMapValues()
    {
        // Act
        WindowsUsbSerialDeviceRecord? record =
            WindowsUsbSerialDeviceRecordParser.Parse(
                "USB-SERIAL CH340 (COM10)",
                "wch.cn",
                @"USB\VID_1A86&PID_7523\CH340ABC123",
                "USB-SERIAL CH340");

        // Assert
        Assert.NotNull(
            record);

        Assert.Equal(
            "COM10",
            record.PortName);

        Assert.Equal(
            (ushort)0x1A86,
            record.VendorId);

        Assert.Equal(
            (ushort)0x7523,
            record.ProductId);

        Assert.Equal(
            "USB-SERIAL CH340",
            record.ProductName);

        Assert.Equal(
            "wch.cn",
            record.ManufacturerName);

        Assert.Equal(
            "CH340ABC123",
            record.SerialNumber);
    }

    [Fact]
    public void Parse_ArduinoUnoRecord_ShouldMapValues()
    {
        // Act
        WindowsUsbSerialDeviceRecord? record =
            WindowsUsbSerialDeviceRecordParser.Parse(
                "Arduino Uno (COM4)",
                "Arduino LLC",
                @"USB\VID_2341&PID_0043\85735313933351D061F1",
                "Arduino Uno");

        // Assert
        Assert.NotNull(
            record);

        Assert.Equal(
            "COM4",
            record.PortName);

        Assert.Equal(
            (ushort)0x2341,
            record.VendorId);

        Assert.Equal(
            (ushort)0x0043,
            record.ProductId);

        Assert.Equal(
            "Arduino Uno",
            record.ProductName);

        Assert.Equal(
            "Arduino LLC",
            record.ManufacturerName);

        Assert.Equal(
            "85735313933351D061F1",
            record.SerialNumber);
    }

    [Fact]
    public void Parse_LowercasePortAndIdentifiers_ShouldNormalizeAndParse()
    {
        // Act
        WindowsUsbSerialDeviceRecord? record =
            WindowsUsbSerialDeviceRecordParser.Parse(
                "USB Serial Device (com10)",
                "Manufacturer",
                @"usb\vid_1a86&pid_7523\abc123",
                "USB Serial Device");

        // Assert
        Assert.NotNull(
            record);

        Assert.Equal(
            "COM10",
            record.PortName);

        Assert.Equal(
            (ushort)0x1A86,
            record.VendorId);

        Assert.Equal(
            (ushort)0x7523,
            record.ProductId);

        Assert.Equal(
            "abc123",
            record.SerialNumber);
    }

    [Fact]
    public void Parse_TopologyDerivedInstance_ShouldNotExposeSerialNumber()
    {
        // Act
        WindowsUsbSerialDeviceRecord? record =
            WindowsUsbSerialDeviceRecordParser.Parse(
                "USB-SERIAL CH340 (COM10)",
                "wch.cn",
                @"USB\VID_1A86&PID_7523\5&2A1B3C4D&0&3",
                "USB-SERIAL CH340");

        // Assert
        Assert.NotNull(
            record);

        Assert.Null(
            record.SerialNumber);
    }

    [Fact]
    public void Parse_NonUsbDeviceInstance_ShouldNotExposeSerialNumber()
    {
        // Act
        WindowsUsbSerialDeviceRecord? record =
            WindowsUsbSerialDeviceRecordParser.Parse(
                "Communications Port (COM1)",
                "Microsoft",
                @"ACPI\PNP0501\1",
                "Communications Port");

        // Assert
        Assert.NotNull(
            record);

        Assert.Null(
            record.VendorId);

        Assert.Null(
            record.ProductId);

        Assert.Null(
            record.SerialNumber);
    }

    [Fact]
    public void Parse_MissingPnpDeviceId_ShouldPreserveCandidate()
    {
        // Act
        WindowsUsbSerialDeviceRecord? record =
            WindowsUsbSerialDeviceRecordParser.Parse(
                "USB Serial Device (COM10)",
                "Manufacturer",
                null,
                "USB Serial Device");

        // Assert
        Assert.NotNull(
            record);

        Assert.Equal(
            "COM10",
            record.PortName);

        Assert.Null(
            record.VendorId);

        Assert.Null(
            record.ProductId);

        Assert.Null(
            record.SerialNumber);
    }

    [Fact]
    public void Parse_InvalidHexIdentifiers_ShouldExposeNullIdentifiers()
    {
        // Act
        WindowsUsbSerialDeviceRecord? record =
            WindowsUsbSerialDeviceRecordParser.Parse(
                "USB Serial Device (COM10)",
                "Manufacturer",
                @"USB\VID_ZZZZ&PID_12X4\ABC123",
                "USB Serial Device");

        // Assert
        Assert.NotNull(
            record);

        Assert.Null(
            record.VendorId);

        Assert.Null(
            record.ProductId);

        Assert.Equal(
            "ABC123",
            record.SerialNumber);
    }

    [Fact]
    public void Parse_IdentifierTextOutsideUsbFields_ShouldNotParse()
    {
        // Act
        WindowsUsbSerialDeviceRecord? record =
            WindowsUsbSerialDeviceRecordParser.Parse(
                "USB Serial Device VID_1A86 (COM10)",
                "Manufacturer",
                @"USB\OTHER_VID_1A86&OTHER_PID_7523\ABC123",
                "USB Serial Device");

        // Assert
        Assert.NotNull(
            record);

        Assert.Null(
            record.VendorId);

        Assert.Null(
            record.ProductId);
    }

    [Fact]
    public void Parse_WhitespaceMetadata_ShouldNormalizeValues()
    {
        // Act
        WindowsUsbSerialDeviceRecord? record =
            WindowsUsbSerialDeviceRecordParser.Parse(
                "USB Serial Device (COM10)",
                " Manufacturer ",
                @"USB\VID_1A86&PID_7523\ABC123",
                " USB Serial Device ");

        // Assert
        Assert.NotNull(
            record);

        Assert.Equal(
            "USB Serial Device",
            record.ProductName);

        Assert.Equal(
            "Manufacturer",
            record.ManufacturerName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Parse_InvalidName_ShouldIgnoreRecord(
        string? name)
    {
        // Act
        WindowsUsbSerialDeviceRecord? record =
            WindowsUsbSerialDeviceRecordParser.Parse(
                name,
                "Manufacturer",
                @"USB\VID_1A86&PID_7523\ABC123",
                "USB Serial Device");

        // Assert
        Assert.Null(
            record);
    }

    [Theory]
    [InlineData("USB Serial Device")]
    [InlineData("USB Serial Device COM10")]
    [InlineData("USB Serial Device [COM10]")]
    [InlineData("USB Serial Device (LPT1)")]
    [InlineData("USB Serial Device (COM)")]
    public void Parse_NameWithoutFinalComSuffix_ShouldIgnoreRecord(
        string name)
    {
        // Act
        WindowsUsbSerialDeviceRecord? record =
            WindowsUsbSerialDeviceRecordParser.Parse(
                name,
                "Manufacturer",
                @"USB\VID_1A86&PID_7523\ABC123",
                "USB Serial Device");

        // Assert
        Assert.Null(
            record);
    }

    [Theory]
    [InlineData("USB Serial Device (COM1)", "COM1")]
    [InlineData("USB Serial Device (COM10)", "COM10")]
    [InlineData("USB Serial Device (COM256)", "COM256")]
    [InlineData("USB Serial Device (COM10) ", "COM10")]
    public void Parse_ValidFinalComSuffix_ShouldExtractPort(
        string name,
        string expectedPortName)
    {
        // Act
        WindowsUsbSerialDeviceRecord? record =
            WindowsUsbSerialDeviceRecordParser.Parse(
                name,
                null,
                null,
                null);

        // Assert
        Assert.NotNull(
            record);

        Assert.Equal(
            expectedPortName,
            record.PortName);
    }

    [Fact]
    public void Parse_EmptyOptionalMetadata_ShouldExposeNull()
    {
        // Act
        WindowsUsbSerialDeviceRecord? record =
            WindowsUsbSerialDeviceRecordParser.Parse(
                "USB Serial Device (COM10)",
                " ",
                @"USB\VID_1A86&PID_7523\ABC123",
                "\t");

        // Assert
        Assert.NotNull(
            record);

        Assert.Null(
            record.ProductName);

        Assert.Null(
            record.ManufacturerName);
    }

    [Theory]
    [InlineData(@"USB\VID_1A86&PID_7523")]
    [InlineData(@"USB\VID_1A86&PID_7523\")]
    [InlineData(@"USB\VID_1A86&PID_7523\ ")]
    [InlineData(@"USB\VID_1A86&PID_7523\5&1234&0&1")]
    [InlineData(@"ROOT\PORTS\0000")]
    public void Parse_UnreliableInstance_ShouldNotExposeSerialNumber(
        string pnpDeviceId)
    {
        // Act
        WindowsUsbSerialDeviceRecord? record =
            WindowsUsbSerialDeviceRecordParser.Parse(
                "USB Serial Device (COM10)",
                null,
                pnpDeviceId,
                null);

        // Assert
        Assert.NotNull(
            record);

        Assert.Null(
            record.SerialNumber);
    }
}