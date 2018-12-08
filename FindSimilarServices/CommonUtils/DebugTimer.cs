using System;
using System.Diagnostics;
using Serilog;
using Serilog.Events;

/// <summary>
/// Debug Timer Class
/// </summary>
public class DebugTimer : IDisposable
{
    private readonly System.Diagnostics.Stopwatch _watch;
    private readonly string _blockName;
    private readonly LogEventLevel _logEventLevel;

    /// <summary>
    /// Creates a timer.
    /// </summary>
    /// <param name="blockName">Name of the block that's being timed</param>
    /// <example>
    /// public void Foo()
    /// {
    ///   using (new DebugTimer("Foo()"))
    ///   {
    ///     // Do work
    ///   }
    /// }
    ///
    /// // In the Visual Studio Output window:
    /// // Foo(): 1.2345 seconds.
    /// </example>
    public DebugTimer(string blockName)
    {
        _blockName = blockName;
        _logEventLevel = LogEventLevel.Debug;
        _watch = Stopwatch.StartNew();
    }

    /// <summary>
    /// Creates a timer.
    /// </summary>
    /// <param name="blockName">Name of the block that's being timed</param>
    /// <param name="logEventLevel">log level to output</param>
    public DebugTimer(string blockName, LogEventLevel logEventLevel)
    {
        _blockName = blockName;
        _logEventLevel = logEventLevel;
        _watch = Stopwatch.StartNew();
    }

    public void Dispose()
    {
        _watch.Stop();
        GC.SuppressFinalize(this);

        switch (_logEventLevel)
        {
            case LogEventLevel.Warning:
                Log.Warning(_blockName + ": " + _watch.Elapsed.TotalSeconds + " seconds.");
                break;
            case LogEventLevel.Information:
                Log.Information(_blockName + ": " + _watch.Elapsed.TotalSeconds + " seconds.");
                break;
            case LogEventLevel.Debug:
                Log.Debug(_blockName + ": " + _watch.Elapsed.TotalSeconds + " seconds.");
                break;
            case LogEventLevel.Verbose:
                Log.Verbose(_blockName + ": " + _watch.Elapsed.TotalSeconds + " seconds.");
                break;
            case LogEventLevel.Fatal:
            default:
                Log.Fatal(_blockName + ": " + _watch.Elapsed.TotalSeconds + " seconds.");
                break;
        }
    }

    ~DebugTimer()
    {
        throw new InvalidOperationException("Must Dispose() of all instances of " + this.GetType().FullName);
    }
}