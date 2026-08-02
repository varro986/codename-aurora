using Aurora.Translation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aurora.Tests.Unit.Translation;

public sealed class DictionaryLoaderTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public DictionaryLoaderTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string WriteTempFile(string content, string name = "dict.json")
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Load_ValidJson_ReturnsDictionary()
    {
        var path = WriteTempFile("""{"hello": "ciao", "world": "mondo"}""");

        var dict = DictionaryLoader.Load(path, NullLogger.Instance);

        Assert.Equal("ciao", dict["hello"]);
        Assert.Equal("mondo", dict["world"]);
    }

    [Fact]
    public void Load_MissingFile_ReturnsEmptyDictionaryWithoutThrowing()
    {
        var path = Path.Combine(_tempDir, "nonexistent.json");

        var dict = DictionaryLoader.Load(path, NullLogger.Instance);

        Assert.Empty(dict);
    }

    [Fact]
    public void Load_MalformedJson_ReturnsEmptyDictionaryWithoutThrowing()
    {
        var path = WriteTempFile("{ this is not valid json }");

        var dict = DictionaryLoader.Load(path, NullLogger.Instance);

        Assert.Empty(dict);
    }

    [Fact]
    public void Load_EmptyJson_ReturnsEmptyDictionary()
    {
        var path = WriteTempFile("{}");

        var dict = DictionaryLoader.Load(path, NullLogger.Instance);

        Assert.Empty(dict);
    }

    [Fact]
    public void Load_CaseInsensitiveKeys()
    {
        var path = WriteTempFile("""{"Hello": "ciao"}""");

        var dict = DictionaryLoader.Load(path, NullLogger.Instance);

        Assert.Equal("ciao", dict["HELLO"]);
        Assert.Equal("ciao", dict["hello"]);
        Assert.Equal("ciao", dict["Hello"]);
    }

    [Fact]
    public void Load_EmptyPath_ReturnsEmptyDictionary()
    {
        var dict = DictionaryLoader.Load("", NullLogger.Instance);

        Assert.Empty(dict);
    }

    [Fact]
    public void Load_NullPath_ReturnsEmptyDictionary()
    {
        var dict = DictionaryLoader.Load(null!, NullLogger.Instance);

        Assert.Empty(dict);
    }

    [Fact]
    public void Load_MultipleEntries_LoadsAll()
    {
        var path = WriteTempFile("""{"a":"1","b":"2","c":"3"}""");

        var dict = DictionaryLoader.Load(path, NullLogger.Instance);

        Assert.Equal(3, dict.Count);
        Assert.Equal("1", dict["a"]);
        Assert.Equal("2", dict["b"]);
        Assert.Equal("3", dict["c"]);
    }
}
