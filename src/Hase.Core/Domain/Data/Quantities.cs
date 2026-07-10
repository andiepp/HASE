namespace Hase.Core.Domain.Data;

public static class Quantities
{
    public static readonly Quantity Frequency = new("frequency", "Frequency");
    public static readonly Quantity Voltage = new("voltage", "Voltage");
    public static readonly Quantity Current = new("current", "Current");
    public static readonly Quantity Temperature = new("temperature", "Temperature");
    public static readonly Quantity Pressure = new("pressure", "Pressure");
    public static readonly Quantity RelativeHumidity = new("relative-humidity", "Relative Humidity");
    public static readonly Quantity Mass = new("mass", "Mass");
    public static readonly Quantity Time = new("time", "Time");
    public static readonly Quantity Length = new("length", "Length");

}