using System;
using System.Diagnostics;
using Serilog;

/// <summary>
/// Debug Timer Class
/// </summary>
public class DebugTimer : IDisposable
{
    private readonly System.Diagnostics.Stopwatch _watch;
    private readonly string _blockName;

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
        _watch = Stopwatch.StartNew();
    }

    public void Dispose()
    {
        _watch.Stop();
        GC.SuppressFinalize(this);
        Log.Debug(_blockName + ": " + _watch.Elapsed.TotalSeconds + " seconds.");
    }

    ~DebugTimer()
    {
        throw new InvalidOperationException("Must Dispose() of all instances of " + this.GetType().FullName);
    }
}