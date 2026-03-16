using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using VsaResults.Features.WideEvents.Unified;
using VsaResults.VsaResult;

namespace VsaResults.Features.Features;

/// <summary>
/// Extension methods for executing features with automatic wide event emission.
/// </summary>
public static class FeatureExecutionExtensions
{
    private static readonly ActivitySource FeatureActivitySource = new("VsaResults.Features");
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> CollectionPropertyCache = new();

    /// <summary>
    /// Executes a mutation feature through the full pipeline:
    /// Validate → Enforce Requirements → Execute Mutation → Run Side Effects → Emit Wide Event.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="feature">The feature to execute.</param>
    /// <param name="request">The request to process.</param>
    /// <param name="emitter">The wide event emitter for observability.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result or errors from execution.</returns>
    public static Task<VsaResult<TResult>> ExecuteAsync<TRequest, TResult>(
        this IMutationFeature<TRequest, TResult> feature,
        TRequest request,
        IWideEventEmitter? emitter = null,
        CancellationToken ct = default)
    {
        var featureName = ResolveFeatureName(feature.GetType());
        var mutator = feature.Mutator ?? throw new InvalidOperationException(ExceptionMessages.MutatorRequired);

        return ExecutePipelineAsync<TRequest, TResult>(
            featureName,
            FeatureTypes.Mutation,
            feature.Validator,
            feature.Requirements,
            (context, token) => mutator.ExecuteAsync(context, token),
            feature.SideEffects,
            request,
            emitter ?? NullWideEventEmitter.Instance,
            ct);
    }

    /// <summary>
    /// Executes a query feature through the pipeline:
    /// Validate → Enforce Requirements → Execute Query → Emit Wide Event.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="feature">The feature to execute.</param>
    /// <param name="request">The request to process.</param>
    /// <param name="emitter">The wide event emitter for observability.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result or errors from execution.</returns>
    public static Task<VsaResult<TResult>> ExecuteAsync<TRequest, TResult>(
        this IQueryFeature<TRequest, TResult> feature,
        TRequest request,
        IWideEventEmitter? emitter = null,
        CancellationToken ct = default)
    {
        var featureName = ResolveFeatureName(feature.GetType());
        var query = feature.Query ?? throw new InvalidOperationException(ExceptionMessages.QueryRequired);

        return ExecutePipelineAsync<TRequest, TResult>(
            featureName,
            FeatureTypes.Query,
            feature.Validator,
            feature.Requirements,
            (context, token) => query.ExecuteAsync(context, token),
            null,
            request,
            emitter ?? NullWideEventEmitter.Instance,
            ct);
    }

    private static async Task<VsaResult<TResult>> ExecutePipelineAsync<TRequest, TResult>(
        string featureName,
        string featureType,
        IFeatureValidator<TRequest> validator,
        IFeatureRequirements<TRequest> requirements,
        Func<FeatureContext<TRequest>, CancellationToken, Task<VsaResult<TResult>>> execute,
        IFeatureSideEffects<TRequest>? sideEffects,
        TRequest request,
        IWideEventEmitter emitter,
        CancellationToken ct)
    {
        using var activity = StartFeatureActivity<TRequest, TResult>(featureName, featureType);
        var builder = WideEvent.StartFeature(featureName, featureType)
            .WithTypes<TRequest, TResult>()
            .WithRequestContext(request)
            .WithPipelineStages(
                validator.GetType(),
                requirements.GetType(),
                execute.Target?.GetType(),
                sideEffects?.GetType(),
                isMutation: featureType == FeatureTypes.Mutation);

        FeatureContext<TRequest>? context = null;

        try
        {
            // Validation stage
            builder.StartStage(StageNames.Validation, validator.GetType(), MethodNames.ValidateAsync);
            var (validated, validationMs) = await ExecuteStageAsync(
                featureName,
                StageNames.Validation,
                () => validator.ValidateAsync(request, ct)).ConfigureAwait(false);
            builder.RecordValidation();
            RecordStageEvent(activity, StageNames.Validation, validationMs);

            if (validated.IsError)
            {
                builder.WithResultContext(validated.Context);
                RecordOutcome(activity, OutcomeNames.ValidationFailure);
                await emitter.EmitAsync(builder.WithFeatureContext(context).ValidationFailure(validated.Errors), ct).ConfigureAwait(false);
                return new VsaResult<TResult>(validated._errors, validated._context);
            }

            ct.ThrowIfCancellationRequested();

            // Requirements stage
            builder.StartStage(StageNames.Requirements, requirements.GetType(), MethodNames.EnforceAsync);
            var (enforced, requirementsMs) = await ExecuteStageAsync(
                featureName,
                StageNames.Requirements,
                () => requirements.EnforceAsync(validated.Value, ct)).ConfigureAwait(false);
            builder.RecordRequirements();
            RecordStageEvent(activity, StageNames.Requirements, requirementsMs);

            if (enforced.IsError)
            {
                builder.WithResultContext(enforced.Context);
                RecordOutcome(activity, OutcomeNames.RequirementsFailure);
                await emitter.EmitAsync(builder.WithFeatureContext(context).RequirementsFailure(enforced.Errors), ct).ConfigureAwait(false);
                return new VsaResult<TResult>(enforced._errors, enforced._context);
            }

            context = enforced.Value;

            // Enrich with request metadata (IP, user agent, etc.)
            EnrichWithRequestMetadata(context);

            ct.ThrowIfCancellationRequested();

            // Execution stage
            builder.StartStage(StageNames.Execution, execute.Target?.GetType(), MethodNames.ExecuteAsync);
            var (result, executionMs) = await ExecuteStageAsync(
                featureName,
                StageNames.Execution,
                () => execute(context, ct)).ConfigureAwait(false);
            builder.RecordExecution();
            RecordStageEvent(activity, StageNames.Execution, executionMs);

            if (result.IsError)
            {
                builder.WithResultContext(result.Context);
                RecordOutcome(activity, OutcomeNames.ExecutionFailure);
                await emitter.EmitAsync(builder.WithFeatureContext(context).ExecutionFailure(result.Errors), ct).ConfigureAwait(false);
                return result;
            }

            // Side effects stage (mutations only)
            // Side effect failures are logged via wide events but do NOT discard the mutation result.
            // The mutation has already committed — returning an error would mislead callers into retrying.
            if (sideEffects != null)
            {
                ct.ThrowIfCancellationRequested();

                builder.StartStage(StageNames.SideEffects, sideEffects.GetType(), MethodNames.ExecuteAsync);
                var (effects, sideEffectsMs) = await ExecuteStageAsync(
                    featureName,
                    StageNames.SideEffects,
                    () => sideEffects.ExecuteAsync(context, ct)).ConfigureAwait(false);
                builder.RecordSideEffects();
                RecordStageEvent(activity, StageNames.SideEffects, sideEffectsMs);

                if (effects.IsError)
                {
                    builder.WithResultContext(effects.Context);
                    RecordOutcome(activity, OutcomeNames.SideEffectsFailure);
                    await emitter.EmitAsync(builder.WithFeatureContext(context).SideEffectsFailure(effects.Errors), ct).ConfigureAwait(false);
                    // Fall through — return the mutation's success result below
                }
            }

            // Auto-inspect result for collection count (only if not already set manually)
            InspectResultCount(context, result.Value);

            // Attach the client-zone receipt to the result context for transport to the response layer
            var receipt = MutationReceipt.FromContext(context);
            if (receipt is not null)
            {
                result = result.WithContext(MutationReceipt.ContextKey, receipt);
            }

            builder.WithResultContext(result.Context);
            RecordOutcome(activity, OutcomeNames.Success);
            await emitter.EmitAsync(builder.WithFeatureContext(context).Success(), ct).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex)
        {
            RecordException(activity, ex);
            await emitter.EmitAsync(builder.WithFeatureContext(context).Exception(ex), ct).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Enriches the feature context with request metadata from the current scope.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <param name="context">The feature context to enrich.</param>
    private static void EnrichWithRequestMetadata<TRequest>(FeatureContext<TRequest> context)
    {
        var provider = RequestMetadataScope.Current;
        if (provider is null)
        {
            return;
        }

        if (provider.IpAddress is { } ip)
        {
            context.AddContext("ip_address", ip);
        }

        if (provider.UserAgent is { } ua)
        {
            context.AddContext("user_agent", ua);
        }

        if (provider.TraceId is { } traceId)
        {
            context.AddContext("request_id", traceId);
        }

        if (provider.RequestPath is { } path)
        {
            context.AddContext("request_path", path);
        }

        if (provider.RequestMethod is { } method)
        {
            context.AddContext("request_method", method);
        }
    }

    private static Activity? StartFeatureActivity<TRequest, TResult>(string featureName, string featureType)
    {
        var activity = FeatureActivitySource.StartActivity($"Feature.{featureType}.{featureName}", ActivityKind.Internal);
        if (activity == null)
        {
            return null;
        }

        activity.SetTag("feature.name", featureName);
        activity.SetTag("feature.type", featureType);
        activity.SetTag("request.type", typeof(TRequest).Name);
        activity.SetTag("result.type", typeof(TResult).Name);
        return activity;
    }

    private static void RecordStageEvent(Activity? activity, string stage, double? durationMs)
    {
        if (activity == null || durationMs is null)
        {
            return;
        }

        var tags = new ActivityTagsCollection
        {
            { "stage", stage },
            { "duration_ms", durationMs.Value },
        };

        activity.AddEvent(new ActivityEvent("feature.stage", tags: tags));
    }

    /// <summary>
    /// Executes a stage within a child span for proper trace visualization.
    /// </summary>
    private static async Task<(TResult Result, double DurationMs)> ExecuteStageAsync<TResult>(
        string featureName,
        string stage,
        Func<Task<TResult>> action)
    {
        using var stageActivity = FeatureActivitySource.StartActivity(
            $"{featureName}.{stage}",
            ActivityKind.Internal);

        stageActivity?.SetTag("feature.stage", stage);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await action().ConfigureAwait(false);
            stopwatch.Stop();

            stageActivity?.SetTag("duration_ms", stopwatch.Elapsed.TotalMilliseconds);
            stageActivity?.SetTag("stage.outcome", "success");

            return (result, stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            stageActivity?.SetTag("duration_ms", stopwatch.Elapsed.TotalMilliseconds);
            stageActivity?.SetTag("stage.outcome", "error");
            stageActivity?.SetTag("error.message", ex.Message);
            throw;
        }
    }

    private static void RecordOutcome(Activity? activity, string outcome)
    {
        activity?.SetTag("feature.outcome", outcome);
    }

    private static void RecordException(Activity? activity, Exception ex)
    {
        if (activity == null)
        {
            return;
        }

        activity.SetTag("feature.outcome", OutcomeNames.Exception);
        activity.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
        {
            { "exception.type", ex.GetType().FullName },
            { "exception.message", ex.Message },
            { "exception.stacktrace", ex.StackTrace },
        }));
    }

    /// <summary>
    /// Inspects the result value for collection count and adds it to the feature context
    /// as "result_count" if not already present. Supports both direct collections and
    /// response objects with a single collection property.
    /// </summary>
    private static void InspectResultCount<TRequest, TResult>(FeatureContext<TRequest>? context, TResult? value)
    {
        if (context is null || value is null)
        {
            return;
        }

        // Don't override manually-set result_count
        if (context.WideEventContext.ContainsKey("result_count"))
        {
            return;
        }

        // Case 1: Result itself is a collection
        if (value is ICollection collection)
        {
            context.AddContext("result_count", collection.Count);
            return;
        }

        if (value is IReadOnlyCollection<object> readOnlyCollection)
        {
            context.AddContext("result_count", readOnlyCollection.Count);
            return;
        }

        // Case 2: Result has a property that is a collection (e.g., Response.Items, Response.Pods)
        var collectionProp = GetCollectionProperty(typeof(TResult));
        if (collectionProp is not null)
        {
            var propValue = collectionProp.GetValue(value);
            if (propValue is ICollection col)
            {
                context.AddContext("result_count", col.Count);
            }
            else if (propValue is IEnumerable)
            {
                // IReadOnlyCollection<T> doesn't have a non-generic .Count, use ICollection or Count property
                var countProp = propValue.GetType().GetProperty("Count");
                if (countProp is not null)
                {
                    context.AddContext("result_count", countProp.GetValue(propValue));
                }
            }
        }
    }

    /// <summary>
    /// Finds the first public property on a type that implements a collection interface.
    /// Cached per type for performance.
    /// </summary>
    private static PropertyInfo? GetCollectionProperty(Type type) =>
        CollectionPropertyCache.GetOrAdd(type, static t =>
        {
            foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var propType = prop.PropertyType;
                if (typeof(ICollection).IsAssignableFrom(propType)
                    || propType.GetInterfaces().Any(i =>
                        i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IReadOnlyCollection<>)))
                {
                    return prop;
                }
            }

            return null;
        });

    private static class FeatureTypes
    {
        public const string Mutation = "Mutation";
        public const string Query = "Query";
    }

    private static class StageNames
    {
        public const string Validation = "validation";
        public const string Requirements = "requirements";
        public const string Execution = "execution";
        public const string SideEffects = "side_effects";
    }

    private static class OutcomeNames
    {
        public const string Success = "success";
        public const string ValidationFailure = "validation_failure";
        public const string RequirementsFailure = "requirements_failure";
        public const string ExecutionFailure = "execution_failure";
        public const string SideEffectsFailure = "side_effects_failure";
        public const string Exception = "exception";
    }

    private static class MethodNames
    {
        public const string ValidateAsync = "ValidateAsync";
        public const string EnforceAsync = "EnforceAsync";
        public const string ExecuteAsync = "ExecuteAsync";
    }

    private static class ExceptionMessages
    {
        public const string MutatorRequired = "Mutator is required for mutation features.";
        public const string QueryRequired = "Query is required for query features.";
    }

    internal static string ResolveFeatureName(Type featureType) =>
        featureType.Name == "Feature" && featureType.DeclaringType is not null
            ? featureType.DeclaringType.Name
            : featureType.Name;
}
