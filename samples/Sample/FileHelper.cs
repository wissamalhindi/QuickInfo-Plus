namespace Sample;

/// <summary>Helpers for working with files.</summary>
/// <example>
/// Create one helper and reuse it:
/// <code>
/// var helper = new FileHelper();
/// helper.Copy("a.txt", "b.txt");
/// </code>
/// </example>
public class FileHelper
{
    /// <summary>Creates a helper that works in the current directory.</summary>
    /// <example>
    /// <code>var helper = new FileHelper();</code>
    /// </example>
    public FileHelper()
    {
        Root = Directory.GetCurrentDirectory();
    }

    /// <summary>Creates a helper that works in <paramref name="root"/>.</summary>
    /// <example>
    /// <code>var helper = new FileHelper(@"C:\temp");</code>
    /// </example>
    public FileHelper(string root)
    {
        Root = root;
    }

    /// <summary>The folder relative paths are resolved against.</summary>
    /// <example>
    /// <code>Console.WriteLine(helper.Root);</code>
    /// </example>
    public string Root { get; set; }

    /// <summary>Copies a file.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="src"/> or <paramref name="dest"/> is null.</exception>
    /// <exception cref="FileNotFoundException"><paramref name="src"/> does not exist. Call <see cref="Exists(string)"/> first.</exception>
    /// <example>
    /// Copy a file next to the original:
    /// <code>
    /// var helper = new FileHelper();
    /// if (helper.Exists("a.txt"))
    /// {
    ///     helper.Copy("a.txt", "b.txt");
    /// }
    /// </code>
    /// </example>
    public void Copy(string src, string dest)
    {
    }

    /// <summary>Copies a file, optionally overwriting the target.</summary>
    /// <example>
    /// <code>helper.Copy("a.txt", "b.txt", overwrite: true);</code>
    /// </example>
    public void Copy(string src, string dest, bool overwrite)
    {
    }

    /// <summary>Checks whether a file exists.</summary>
    public bool Exists(string path) => File.Exists(path);

    /// <summary>Archives a file.</summary>
    /// <example>
    /// Steps:<br/>The helper does the following:
    /// <list type="bullet">
    /// <item><description>Copies the file to the archive folder.</description></item>
    /// <item><description>Deletes the original.</description></item>
    /// </list>
    /// Returns an <see cref="ArchiveResult"/>.
    /// See <see cref="Exists(string)"> the exists check </see> first. Limit: 10&#160;MB per file.
    /// <code>var result = helper.Archive("a.txt");
    /// if (result.Moved)
    /// {
    ///     Console.WriteLine(result.Target);
    /// }
    /// </code>
    /// </example>
    public ArchiveResult Archive(string path) => new(path, true);

    /// <summary>Finds files by a long filter chain (layout test: very long code line).</summary>
    /// <example>
    /// One very long line:
    /// <code>
    /// var matches = helper.FindAll(Root).Where(f => f.EndsWith(".txt") || f.EndsWith(".log") || f.EndsWith(".csv")).Select(f => new { Name = Path.GetFileName(f), Size = new FileInfo(f).Length, Modified = File.GetLastWriteTime(f) }).OrderByDescending(f => f.Modified).ThenBy(f => f.Name).Take(100).ToList();
    /// </code>
    /// </example>
    public IEnumerable<string> FindAll(string folder) => Array.Empty<string>();

    /// <summary>Migrates a folder (layout test: tall tooltip that needs scrolling).</summary>
    /// <exception cref="ArgumentNullException"><paramref name="folder"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="folder"/> is empty or contains invalid characters.</exception>
    /// <exception cref="DirectoryNotFoundException"><paramref name="folder"/> does not exist.</exception>
    /// <exception cref="UnauthorizedAccessException">The caller has no write access to <paramref name="folder"/>.</exception>
    /// <exception cref="IOException">A file in <paramref name="folder"/> is locked by another process.</exception>
    /// <exception cref="PathTooLongException">A path inside <paramref name="folder"/> is too long.</exception>
    /// <example>
    /// Migrate every file, logging each step:
    /// <code>
    /// var helper = new FileHelper();
    /// var log = new List&lt;string&gt;();
    /// foreach (var file in helper.FindAll("data"))
    /// {
    ///     log.Add($"start {file}");
    ///     if (!helper.Exists(file))
    ///     {
    ///         log.Add($"missing {file}");
    ///         continue;
    ///     }
    ///
    ///     var target = Path.Combine("migrated", Path.GetFileName(file));
    ///     helper.Copy(file, target);
    ///     log.Add($"copied {file}");
    ///
    ///     var lines = helper.ReadLines(target);
    ///     for (var i = 0; i &lt; lines.Count; i++)
    ///     {
    ///         lines[i] = lines[i].Trim();
    ///     }
    ///
    ///     helper.Append("migration.log", string.Join(Environment.NewLine, log));
    ///     log.Clear();
    /// }
    ///
    /// Console.WriteLine("Migration step 1 done");
    /// Console.WriteLine("Migration step 2 done");
    /// Console.WriteLine("Migration step 3 done");
    /// Console.WriteLine("Migration step 4 done");
    /// Console.WriteLine("Migration step 5 done");
    /// Console.WriteLine("Migration step 6 done");
    /// Console.WriteLine("Migration step 7 done");
    /// Console.WriteLine("Migration step 8 done");
    /// Console.WriteLine("Migration step 9 done");
    /// Console.WriteLine("Migration step 10 done");
    /// Console.WriteLine("Migration step 11 done");
    /// Console.WriteLine("Migration step 12 done");
    /// Console.WriteLine("Migration step 13 done");
    /// Console.WriteLine("Migration step 14 done");
    /// Console.WriteLine("Migration step 15 done");
    /// Console.WriteLine("Migration step 16 done");
    /// Console.WriteLine("Migration step 17 done");
    /// Console.WriteLine("Migration step 18 done");
    /// Console.WriteLine("Migration step 19 done");
    /// Console.WriteLine("Migration step 20 done");
    /// </code>
    /// Then check the log:
    /// <code>
    /// foreach (var line in helper.ReadLines("migration.log"))
    /// {
    ///     Console.WriteLine(line);
    /// }
    /// </code>
    /// </example>
    public void Migrate(string folder)
    {
    }

    /// <summary>Renames a file.</summary>
    /// <exception cref="IOException">A file named <paramref name="newName"/> already exists.</exception>
    /// <exception cref="UnauthorizedAccessException"/>
    public void Rename(string path, string newName)
    {
    }

    /// <summary>Moves a file.</summary>
    /// <example>
    /// Move within a folder:
    /// <code>helper.Move("a.txt", "b.txt");</code>
    /// </example>
    /// <example>
    /// Move to another folder:
    /// <code>helper.Move("a.txt", @"archive\a.txt");</code>
    /// </example>
    public void Move(string src, string dest)
    {
    }

    /// <summary>Reads all lines of a file.</summary>
    /// <example>
    /// <code>List&lt;string&gt; lines = helper.ReadLines("a.txt");</code>
    /// </example>
    public List<string> ReadLines(string path) => new();

    /// <summary>Returns the value unchanged.</summary>
    /// <example>
    /// Returns the same <typeparamref name="T"/> you pass in:
    /// <code>int x = FileHelper.Map&lt;int&gt;(1);</code>
    /// </example>
    public static T Map<T>(T value) => value;

    /// <summary>Deletes a file.</summary>
    /// <example>
    /// Pass <paramref name="path"/> relative to <see cref="Root"/>. Returns <c>false</c> when the file
    /// does not exist, and never throws for <see langword="null"/> paths. See
    /// <see href="https://learn.microsoft.com/dotnet/api/system.io.file.delete">File.Delete</see> for details.
    /// </example>
    public bool Delete(string? path) => path != null;

    /// <summary>Appends text to a file.</summary>
    /// <example>
    /// <para>Appending creates the file if it is missing.</para>
    /// <para>Call <see cref="Exists(string)"/> first if you need to know whether it was created:</para>
    /// <code>
    /// bool existed = helper.Exists("log.txt");
    /// helper.Append("log.txt", "started");
    ///
    /// if (!existed)
    /// {
    ///     Console.WriteLine("log.txt created");
    /// }
    /// </code>
    /// </example>
    public void Append(string path, string text)
    {
    }

    /// <summary>Reads a file asynchronously.</summary>
    /// <example>
    /// <code>
    /// string text = await helper.ReadAsync("a.txt", CancellationToken.None);
    /// </code>
    /// </example>
    public Task<string> ReadAsync(string path, CancellationToken cancellationToken) => Task.FromResult("");

    /// <summary>Copies every file that matches a pattern.</summary>
    /// <example>
    /// This example is deliberately long so you can see how prose wraps in the tooltip. It copies every
    /// text file from the source folder to the target folder, keeping the folder structure, skipping files
    /// that already exist in the target, and reporting how many files were copied when it finishes.
    /// <code>
    /// int copied = helper.CopyAll("src", "dest", "*.txt");
    /// Console.WriteLine($"Copied {copied} files");
    /// </code>
    /// </example>
    public int CopyAll(string sourceFolder, string targetFolder, string pattern) => 0;

    /// <summary>Opens a file for writing.</summary>
    /// <example>
    /// <code>
    /// using (var stream = helper.OpenWrite("a.txt"))
    /// using (var writer = new StreamWriter(stream))
    /// {
    ///     foreach (var line in new[] { "one", "two" })
    ///     {
    ///         if (line.Length &gt; 0 &amp;&amp; line != "skip")
    ///         {
    ///             writer.WriteLine(line);
    ///         }
    ///     }
    /// }
    /// </code>
    /// </example>
    public Stream OpenWrite(string path) => Stream.Null;
}

/// <summary>A cache keyed by file path.</summary>
/// <typeparam name="T">The cached value type.</typeparam>
/// <example>
/// <code>
/// var cache = new FileCache&lt;string&gt;();
/// cache.Set("a.txt", "contents");
/// </code>
/// </example>
public class FileCache<T>
{
    private readonly Dictionary<string, T> _items = new();

    /// <summary>Stores a value.</summary>
    /// <example>
    /// Overwrites any value already stored for <paramref name="path"/> as a <typeparamref name="T"/>:
    /// <code>cache.Set("a.txt", value);</code>
    /// </example>
    public void Set(string path, T value) => _items[path] = value;
}

/// <summary>Where copied files go when no target is given.</summary>
/// <example>
/// <code>var target = CopyTarget.Backup;</code>
/// </example>
public enum CopyTarget
{
    /// <summary>The same folder.</summary>
    SameFolder,

    /// <summary>A backup folder.</summary>
    /// <example>
    /// <code>helper.CopyAll("src", CopyTarget.Backup.ToString(), "*");</code>
    /// </example>
    Backup,
}

public static class StringExtensions
{
    /// <summary>Upper-cases a string.</summary>
    /// <example>
    /// <code>"hello".Shout(); // "HELLO"</code>
    /// </example>
    public static string Shout(this string s) => s.ToUpperInvariant();
}

/// <summary>Result of <see cref="FileHelper.Archive(string)"/>.</summary>
public record ArchiveResult(string Target, bool Moved);
