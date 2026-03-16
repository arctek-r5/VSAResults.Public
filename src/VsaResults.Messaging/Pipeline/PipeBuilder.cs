using VsaResults.Features.Features;
using VsaResults.VsaResult;

namespace VsaResults.Messaging.Pipeline;

/// <summary>
/// Fluent builder for constructing pipelines.
/// </summary>
/// <typeparam name="TContext">The context type.</typeparam>
/// <remarks>
/// <para>
/// Example:
/// <code>
/// var pipe = new PipeBuilder&lt;MessagePipeContext&lt;OrderCreated&gt;&gt;()
///     .UseFilter(new LoggingFilter())
///     .UseFilter(new RetryFilter(RetryPolicy.Exponential(3, TimeSpan.FromSeconds(1))))
///     .UseFilter(new TimeoutFilter(TimeSpan.FromSeconds(30)))
///     .Build(consumerFilter);
/// </code>
/// </para>
/// </remarks>
public sealed class PipeBuilder<TContext>
    where TContext : PipeContext
{
    private readonly List<IFilter<TContext>> _filters = [];

    /// <summary>
    /// Adds a filter to the pipeline.
    /// </summary>
    /// <param name="filter">The filter to add.</param>
    /// <returns>This builder for chaining.</returns>
    public PipeBuilder<TContext> UseFilter(IFilter<TContext> filter)
    {
        _filters.Add(filter);
        return this;
    }

    /// <summary>
    /// Adds a filter using a factory function.
    /// </summary>
    /// <typeparam name="TFilter">The filter type.</typeparam>
    /// <param name="factory">Factory to create the filter.</param>
    /// <returns>This builder for chaining.</returns>
    public PipeBuilder<TContext> UseFilter<TFilter>(Func<TFilter> factory)
        where TFilter : IFilter<TContext>
    {
        _filters.Add(factory());
        return this;
    }

    /// <summary>
    /// Adds a delegate filter to the pipeline.
    /// </summary>
    /// <param name="filterAction">The filter action.</param>
    /// <param name="filterName">Optional name for diagnostics.</param>
    /// <returns>This builder for chaining.</returns>
    public PipeBuilder<TContext> Use(
        Func<TContext, IPipe<TContext>, CancellationToken, Task<VsaResult<Unit>>> filterAction,
        string? filterName = null)
    {
        _filters.Add(new DelegateFilter<TContext>(filterAction, filterName ?? "DelegateFilter"));
        return this;
    }

    /// <summary>
    /// Conditionally adds a filter based on a predicate.
    /// </summary>
    /// <param name="condition">Whether to add the filter.</param>
    /// <param name="filter">The filter to add.</param>
    /// <returns>This builder for chaining.</returns>
    public PipeBuilder<TContext> UseFilterIf(bool condition, IFilter<TContext> filter)
    {
        if (condition)
        {
            _filters.Add(filter);
        }

        return this;
    }

    /// <summary>
    /// Conditionally adds a filter based on a predicate.
    /// </summary>
    /// <param name="condition">Whether to add the filter.</param>
    /// <param name="filterFactory">Factory to create the filter.</param>
    /// <returns>This builder for chaining.</returns>
    public PipeBuilder<TContext> UseFilterIf(bool condition, Func<IFilter<TContext>> filterFactory)
    {
        if (condition)
        {
            _filters.Add(filterFactory());
        }

        return this;
    }

    /// <summary>
    /// Inserts a filter at a specific position.
    /// </summary>
    /// <param name="index">The position to insert at.</param>
    /// <param name="filter">The filter to insert.</param>
    /// <returns>This builder for chaining.</returns>
    public PipeBuilder<TContext> InsertFilter(int index, IFilter<TContext> filter)
    {
        _filters.Insert(index, filter);
        return this;
    }

    /// <summary>
    /// Clears all filters from the builder.
    /// </summary>
    /// <returns>This builder for chaining.</returns>
    public PipeBuilder<TContext> Clear()
    {
        _filters.Clear();
        return this;
    }

    /// <summary>
    /// Gets the current filter count.
    /// </summary>
    public int FilterCount => _filters.Count;

    /// <summary>
    /// Builds the pipeline.
    /// </summary>
    /// <returns>The composed pipeline.</returns>
    public IPipe<TContext> Build()
    {
        if (_filters.Count == 0)
        {
            return EmptyPipe<TContext>.Instance;
        }

        return new ComposedPipe<TContext>(_filters.ToList());
    }

    /// <summary>
    /// Builds the pipeline with a terminal filter.
    /// </summary>
    /// <param name="terminalFilter">The filter to execute last.</param>
    /// <returns>The composed pipeline.</returns>
    public IPipe<TContext> Build(IFilter<TContext> terminalFilter)
    {
        var filters = new List<IFilter<TContext>>(_filters)
        {
            terminalFilter
        };

        return new ComposedPipe<TContext>(filters);
    }
}

/// <summary>
/// Filter that wraps a delegate function.
/// </summary>
internal sealed class DelegateFilter<TContext> : IFilter<TContext>
    where TContext : PipeContext
{
    private readonly Func<TContext, IPipe<TContext>, CancellationToken, Task<VsaResult<Unit>>> _action;
    private readonly string _name;

    public DelegateFilter(
        Func<TContext, IPipe<TContext>, CancellationToken, Task<VsaResult<Unit>>> action,
        string name)
    {
        _action = action;
        _name = name;
    }

    public Task<VsaResult<Unit>> SendAsync(TContext context, IPipe<TContext> next, CancellationToken ct = default)
        => _action(context, next, ct);

    public void Probe(ProbeContext context) => context.Add(_name);
}

/// <summary>
/// Extensions for building pipes.
/// </summary>
public static class PipeBuilderExtensions
{
    /// <summary>
    /// Creates a new pipe builder.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <returns>A new pipe builder.</returns>
    public static PipeBuilder<TContext> CreatePipe<TContext>()
        where TContext : PipeContext
        => new();
}
