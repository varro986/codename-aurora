using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Aurora.EndToEnd.Tests.Support;

namespace Aurora.EndToEnd.Tests;

public sealed class NoNetworkTests
{
    // AC-2.4 (static part): the code of Aurora references no network API. A run with the network blocked is by hand.
    [Fact]
    [Trait("Category", "Desktop")]
    public void Aurora_assemblies_reference_no_System_Net_assembly()
    {
        var assemblies = Directory.GetFiles(AuroraProcess.OutputFolder(), "Aurora*.dll");
        Assert.NotEmpty(assemblies);

        var offending = assemblies.SelectMany(path => ReferencesOf(path)
            .Where(name => name.StartsWith("System.Net", StringComparison.Ordinal))
            .Select(name => $"{Path.GetFileName(path)} -> {name}"));

        Assert.Empty(offending);
    }

    private static IEnumerable<string> ReferencesOf(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        return [.. reader.AssemblyReferences.Select(handle => reader.GetString(reader.GetAssemblyReference(handle).Name))];
    }
}
