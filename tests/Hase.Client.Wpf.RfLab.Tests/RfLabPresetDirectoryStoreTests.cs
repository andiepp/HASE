using System.IO;
using Hase.Client.Wpf.RfLab.Presets;

namespace Hase.Client.Wpf.RfLab.Tests;

/// <summary>
/// The preset store reads a directory the composing application names. A
/// directory that is missing or unreadable yields no presets rather than
/// preventing the panel from opening.
/// </summary>
public sealed class RfLabPresetDirectoryStoreTests : IDisposable
{
    private readonly string directoryPath;

    public RfLabPresetDirectoryStoreTests()
    {
        directoryPath = Path.Combine(
            Path.GetTempPath(),
            "hase-rflab-presets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(directoryPath, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void TheStore_ShouldListPresetsByNameInOrder()
    {
        Write("zulu", "Frequency,1000000");
        Write("alpha", "Frequency,2000000");
        Write("mike", "Frequency,3000000");

        var store = new RfLabPresetDirectoryStore(directoryPath);

        Assert.Equal(["alpha", "mike", "zulu"], store.ListNames());
    }

    [Fact]
    public void TheStore_ShouldIgnoreFilesThatAreNotPresets()
    {
        Write("kept", "Frequency,1000000");
        File.WriteAllText(Path.Combine(directoryPath, "notes.md"), "not a preset");
        File.WriteAllText(Path.Combine(directoryPath, "archive.zip"), "not a preset");

        var store = new RfLabPresetDirectoryStore(directoryPath);

        Assert.Equal(["kept"], store.ListNames());
    }

    [Fact]
    public void AMissingDirectory_ShouldYieldNoPresetsRatherThanFail()
    {
        var store = new RfLabPresetDirectoryStore(
            Path.Combine(directoryPath, "does-not-exist"));

        // The panel must still open when the operator has no presets yet.
        Assert.Empty(store.ListNames());
        Assert.Null(store.Read("anything"));
    }

    [Fact]
    public void TheStore_ShouldReadAListedPreset()
    {
        Write("bench", "Frequency,21400000", "Amplitude,40");

        var store = new RfLabPresetDirectoryStore(directoryPath);
        RfLabPreset? preset = store.Read("bench");

        Assert.NotNull(preset);
        Assert.Equal("bench", preset!.Name);
        Assert.Equal(21_400_000, preset.Frequency);
        Assert.Equal(40, preset.Amplitude);
    }

    [Fact]
    public void AnUnknownName_ShouldReadAsNothing()
    {
        var store = new RfLabPresetDirectoryStore(directoryPath);

        Assert.Null(store.Read("never written"));
        Assert.Null(store.Read(string.Empty));
        Assert.Null(store.Read("   "));
    }

    [Theory]
    [InlineData("..\\outside")]
    [InlineData("../outside")]
    [InlineData("sub\\nested")]
    public void ANameThatLeavesTheDirectory_ShouldBeRefused(string name)
    {
        // A name is an entry the store listed, so it resolves to a file
        // directly inside the directory and nowhere else.
        Write("outside", "Frequency,1000000");
        var store = new RfLabPresetDirectoryStore(directoryPath);

        Assert.Null(store.Read(name));
    }

    [Fact]
    public void TheDefaultDirectory_ShouldSitBesideTheClientConfiguration()
    {
        // An application update replaces the program and preserves the
        // configuration, so presets kept there survive it.
        string expected = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "HASE",
            "Client",
            "Configuration",
            "RfLabPresets");

        Assert.Equal(expected, RfLabPresetDirectoryStore.DefaultDirectoryPath);
    }

    private void Write(string name, params string[] lines) =>
        File.WriteAllLines(
            Path.Combine(directoryPath, name + ".txt"),
            lines);
}
