using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CyberManager.Server.Logging;

/// <summary>
/// Guarda los registros del servidor en archivos diarios junto al ejecutable.
/// Los archivos se abren en modo <c>append</c>, así que al reiniciar se conserva
/// todo lo anterior. Los archivos con más de <c>retentionDays</c> días se eliminan
/// automáticamente al arrancar y al cambiar de día.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    /// <summary>Tamaño máximo de un archivo antes de pasar al siguiente (<c>.1</c>, <c>.2</c>...).</summary>
    private const long MaxFileBytes = 32L * 1024 * 1024;

    private static readonly Regex FileDate = new(@"^server-(\d{4}-\d{2}-\d{2})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly string _folder;
    private readonly int _retentionDays;
    private readonly BlockingCollection<string> _pending =
        new(new ConcurrentQueue<string>(), 20_000);
    private readonly Task _writer;
    private readonly object _sync = new();

    private StreamWriter? _stream;
    private DateOnly _day;
    private int _part;

    public FileLoggerProvider(string folder, int retentionDays = 30)
    {
        _folder = folder;
        _retentionDays = Math.Max(1, retentionDays);

        try { Directory.CreateDirectory(_folder); } catch { /* sin permisos: se descarta */ }

        // La limpieza nunca toca el archivo del día en curso.
        PurgeOldFiles();
        _writer = Task.Run(WriteLoop);
    }

    public string Folder => _folder;

    public int RetentionDays => _retentionDays;

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    /// <summary>Borra los archivos diarios anteriores al periodo de retención.</summary>
    public void PurgeOldFiles()
    {
        try
        {
            var limit = DateOnly.FromDateTime(DateTime.Now).AddDays(-_retentionDays);
            foreach (var file in Directory.EnumerateFiles(_folder, "server-*.log"))
            {
                var match = FileDate.Match(Path.GetFileName(file));
                if (!match.Success) continue;

                if (!DateOnly.TryParseExact(match.Groups[1].Value, "yyyy-MM-dd",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
                    continue;

                if (day >= limit) continue;

                try { File.Delete(file); } catch { /* puede estar abierto en otro proceso */ }
            }
        }
        catch { /* la retención no debe afectar al servidor */ }
    }

    internal void Enqueue(string line)
    {
        if (!_pending.IsAddingCompleted)
            _pending.TryAdd(line);
    }

    private void WriteLoop()
    {
        foreach (var line in _pending.GetConsumingEnumerable())
        {
            try
            {
                var today = DateOnly.FromDateTime(DateTime.Now);
                lock (_sync)
                {
                    if (today != _day)
                    {
                        Close();
                        _day = today;
                        _part = 0;
                        PurgeOldFiles();
                    }

                    // Al abrir por primera vez se usa el archivo base del día en modo
                    // "append"; si ya superó el tamaño máximo se pasa a .1, .2, etc.
                    var path = FilePath(_day, _part);
                    if (_stream is null)
                    {
                        Open(path);
                    }
                    else if (File.Exists(path) && new FileInfo(path).Length >= MaxFileBytes)
                    {
                        Close();
                        path = FilePath(_day, ++_part);
                        Open(path);
                    }

                    _stream!.WriteLine(line);
                }
            }
            catch
            {
                // El registro nunca debe tumbar el servidor.
            }
        }
    }

    private string FilePath(DateOnly day, int part)
    {
        var name = part <= 0
            ? $"server-{day:yyyy-MM-dd}.log"
            : $"server-{day:yyyy-MM-dd}.{part}.log";
        return Path.Combine(_folder, name);
    }

    private void Open(string path)
    {
        // Si alguien borra la carpeta mientras el servidor corre, se vuelve a crear.
        try { Directory.CreateDirectory(_folder); } catch { }
        var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        _stream = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
    }

    private void Close()
    {
        try { _stream?.Dispose(); } catch { /* archivo bloqueado */ }
        _stream = null;
    }

    public void Dispose()
    {
        if (_pending.IsAddingCompleted) return;

        _pending.CompleteAdding();
        try { _writer.Wait(TimeSpan.FromSeconds(5)); } catch { }

        lock (_sync) { Close(); }
        _pending.Dispose();
    }

    private sealed class FileLogger : ILogger
    {
        private readonly FileLoggerProvider _owner;
        private readonly string _category;

        public FileLogger(FileLoggerProvider owner, string category)
        {
            _owner = owner;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var message = formatter(state, exception);
            var line = new StringBuilder(message.Length + 128);
            line.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff",
                CultureInfo.InvariantCulture));
            line.Append(" [").Append(LevelName(logLevel)).Append("] ");
            line.Append(_category).Append(": ").Append(message);

            if (exception is not null)
                line.AppendLine().Append(exception);

            _owner.Enqueue(line.ToString());
        }

        private static string LevelName(LogLevel level) => level switch
        {
            LogLevel.Trace => "TRACE",
            LogLevel.Debug => "DEBUG",
            LogLevel.Information => "INFO",
            LogLevel.Warning => "WARN",
            LogLevel.Error => "ERROR",
            LogLevel.Critical => "CRITICAL",
            _ => "NONE"
        };
    }
}
