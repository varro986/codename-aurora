using System.Diagnostics;
using System.Text;

namespace Aurora.App;

/// <summary>
/// Appends timestamped lines to the configured log file; safe from any thread and it never throws: a log that cannot
/// be written goes to the debugger instead. Past <paramref name="maxBytes"/> the file is rolled over to
/// "name.1.log", replacing the previous one, so the log is bounded to about twice that size.
/// </summary>
internal sealed class FileLog(string path, long maxBytes)
{
    private readonly Lock _lock = new();
    private readonly string _rolledPath = System.IO.Path.Combine(
        System.IO.Path.GetDirectoryName(path)!,
        $"{System.IO.Path.GetFileNameWithoutExtension(path)}.1{System.IO.Path.GetExtension(path)}");

    private bool _folderReady;
    private long? _size;

    public string Path { get; } = path;

    public void Info(string message) => Write("INFO", message);

    public void Error(string message, Exception? exception = null) =>
        Write("ERROR", exception is null ? message : $"{message}{Environment.NewLine}{exception}");

    private void Write(string level, string message)
    {
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} [{level}] {message}{Environment.NewLine}";
        lock (_lock)
        {
            try
            {
                if (!_folderReady)
                {
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
                    _folderReady = true;
                }

                var size = _size ??= File.Exists(Path) ? new FileInfo(Path).Length : 0;
                if (size >= maxBytes)
                {
                    File.Move(Path, _rolledPath, overwrite: true);
                    size = 0;
                }

                File.AppendAllText(Path, line);
                _size = size + Encoding.UTF8.GetByteCount(line);
            }
            catch (Exception ex)
            {
                _size = null;
                Debug.WriteLine($"Aurora log not written ({ex.Message}): {line}");
            }
        }
    }
}
