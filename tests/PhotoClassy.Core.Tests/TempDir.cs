namespace PhotoClassy.Core.Tests;

/// <summary>테스트용 임시 폴더 (Dispose 시 삭제).</summary>
public sealed class TempDir : IDisposable
{
    public TempDir()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PhotoClassyTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Combine(params string[] parts) => System.IO.Path.Combine(new[] { Path }.Concat(parts).ToArray());

    public string CreateFile(string relativePath, string content, DateTime? modified = null)
    {
        var full = Combine(relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        if (modified.HasValue)
            File.SetLastWriteTime(full, modified.Value);
        return full;
    }

    public void Dispose()
    {
        try { Directory.Delete(Path, true); } catch { /* ignore */ }
    }
}
