using VsaResults.Features.Features;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.Pipeline;

/// <summary>
/// A composed pipeline of filters.
/// Pipes represent a chain of filters that process contexts sequentially.
/// </summary>
/// <typeparam name="TContext">The context type.</typeparam>
public interface IPipe<TContext>
    where TContext : PipeContext
{
    /// <summary>
    /// Sends the context through the pipeline.
    /// </summary>
    /// <param name="context">The context to process.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Unit on success, or errors on failure.</returns>
    Task<VsaResult<Unit>> SendAsync(TContext context, CancellationToken ct = default);

    /// <summary>
    /// Probes the pipeline for diagnostic information.
    /// </summary>
    /// <param name="context">The probe context.</param>
    void Probe(ProbeContext context);
}

/// <summary>
/// Empty pipe that does nothing - used as the terminal pipe in a chain.
/// </summary>
/// <typeparam name="TContext">The context type.</typeparam>
internal sealed class EmptyPipe<TContext> : IPipe<TContext>
    where TContext : PipeContext
{
    /// <summary>Gets the singleton instance.</summary>
    public static EmptyPipe<TContext> Instance { get; } = new();

    private EmptyPipe()
    {
    }

    /// <inheritdoc />
    public Task<VsaResult<Unit>> SendAsync(TContext context, CancellationToken ct = default)
        => Task.FromResult<VsaResult<Unit>>(Unit.Value);

    /// <inheritdoc />
    public void Probe(ProbeContext context)
    {
        // Empty pipe adds nothing to the probe
    }
}

/// <summary>
/// Pipe that executes a composed chain of filters.
/// </summary>
/// <typeparam name="TContext">The context type.</typeparam>
internal sealed class ComposedPipe<TContext> : IPipe<TContext>
    where TContext : PipeContext
{
    private readonly IReadOnlyList<IFilter<TContext>> _filters;

    public ComposedPipe(IReadOnlyList<IFilter<TContext>> filters)
    {
        _filters = filters;
    }

    /// <inheritdoc />
    public Task<VsaResult<Unit>> SendAsync(TContext context, CancellationToken ct = default)
    {
        if (_filters.Count == 0)
        {
            return Task.FromResult<VsaResult<Unit>>(Unit.Value);
        }

        return ExecuteFilter(0, context, ct);
    }

    private Task<VsaResult<Unit>> ExecuteFilter(int index, TContext context, CancellationToken ct)
    {
        if (index >= _filters.Count)
        {
            return Task.FromResult<VsaResult<Unit>>(Unit.Value);
        }

        var filter = _filters[index];
        var nextPipe = new NextPipe(this, index + 1);
        return filter.SendAsync(context, nextPipe, ct);
    }

    /// <inheritdoc />
    public void Probe(ProbeContext context)
    {
        foreach (var filter in _filters)
        {
            filter.Probe(context);
        }
    }

    private sealed class NextPipe : IPipe<TContext>
    {
        private readonly ComposedPipe<TContext> _pipe;
        private readonly int _nextIndex;

        public NextPipe(ComposedPipe<TContext> pipe, int nextIndex)
        {
            _pipe = pipe;
            _nextIndex = nextIndex;
        }

        public Task<VsaResult<Unit>> SendAsync(TContext context, CancellationToken ct = default)
            => _pipe.ExecuteFilter(_nextIndex, context, ct);

        public void Probe(ProbeContext context)
        {
            for (var i = _nextIndex; i < _pipe._filters.Count; i++)
            {
                _pipe._filters[i].Probe(context);
            }
        }
    }
}
