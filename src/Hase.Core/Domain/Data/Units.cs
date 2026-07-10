namespace Hase.Core.Domain.Data;

public static class Units
{
    public static readonly Unit Hertz =
        new("hertz", "Hertz", "Hz", Quantities.Frequency);

    public static readonly Unit Kilohertz =
        new("kilohertz", "Kilohertz", "kHz", Quantities.Frequency);

    public static readonly Unit Megahertz =
        new("megahertz", "Megahertz", "MHz", Quantities.Frequency);

    public static readonly Unit Volt =
        new("volt", "Volt", "V", Quantities.Voltage);

    public static readonly Unit Millivolt =
        new("millivolt", "Millivolt", "mV", Quantities.Voltage);

    public static readonly Unit Ampere =
        new("ampere", "Ampere", "A", Quantities.Current);

    public static readonly Unit Milliampere =
        new("milliampere", "Milliampere", "mA", Quantities.Current);

    public static readonly Unit Kelvin =
        new("kelvin", "Kelvin", "K", Quantities.Temperature);

    public static readonly Unit Celsius =
        new("celsius", "Degree Celsius", "°C", Quantities.Temperature);

    public static readonly Unit PercentRelativeHumidity =
        new(
            "percent-relative-humidity",
            "Percent Relative Humidity",
            "%RH",
            Quantities.RelativeHumidity);

    public static readonly Unit Hectopascal =
        new(
            "hectopascal",
            "Hectopascal",
            "hPa",
            Quantities.Pressure);
}