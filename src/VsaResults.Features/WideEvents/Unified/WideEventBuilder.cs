using System.Collections.Concurrent;
using System.Diagnostics;
using VsaResults.Errors;
using VsaResults.Features.Features;
using VsaResults.Features.WideEvents.Unified.Segments;

namespace VsaResults.Features.WideEvents.Unified;

/// <summary>
/// Builder for constructing unified wide events throughout the operation lifecycle.
/// </summary>
public sealed partial class WideEventBuilder
{
    // Cached environment info - read once at first use, never changes during runtime
    private static readonly Lazy<EnvironmentInfo> CachedEnvironment = new(
        () => new EnvironmentInfo
        {
            ServiceName = Environment.GetEnvironmentVariable("SERVICE_NAME"),
            ServiceVersion = Environment.GetEnvironmentVariable("SERVICE_VERSION"),
            CommitHash = Environment.GetEnvironmentVariable("COMMIT_SHA")
                ?? Environment.GetEnvironmentVariable("GIT_COMMIT"),
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT"),
            DeploymentId = Environment.GetEnvironmentVariable("DEPLOYMENT_ID"),
            Region = Environment.GetEnvironmentVariable("AZURE_REGION")
                ?? Environment.GetEnvironmentVariable("AWS_REGION"),
            Host = Environment.MachineName,
        },
        LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly WideEvent _event;
    private readonly Stopwatch _totalStopwatch;
    private readonly Stopwatch _stageStopwatch;
    private string _currentStage = "unknown";
    private string? _currentNamespace;
    private string? _currentClass;
    private string? _currentMethod;

    /// <summary>
    /// Initializes a new instance of the <see cref="WideEventBuilder"/> class.
    /// </summary>
    /// <param name="eventType">The type of event being built.</param>
    public WideEventBuilder(string eventType)
    {
        _totalStopwatch = Stopwatch.StartNew();
        _stageStopwatch = new Stopwatch();

        var activity = Activity.Current;
        var env = CachedEnvironment.Value;

        _event = new WideEvent
        {
            EventType = eventType,
            Outcome = "pending",
            TraceId = activity?.TraceId.ToString(),
            SpanId = activity?.SpanId.ToString(),
            ParentSpanId = activity?.ParentSpanId.ToString(),
            ServiceName = env.ServiceName,
            ServiceVersion = env.ServiceVersion,
            CommitHash = env.CommitHash,
            Environment = env.Environment,
            DeploymentId = env.DeploymentId,
            Region = env.Region,
            Host = env.Host,
        };
    }

    /// <summary>
    /// Gets the underlying wide event being built.
    /// Useful for scope integration.
    /// </summary>
    internal WideEvent Event => _event;

    /// <summary>
    /// Adds a feature segment to the wide event.
    /// </summary>
    /// <param name="featureName">The name of the feature.</param>
    /// <param name="featureType">The type of feature ("Mutation" or "Query").</param>
    /// <returns>This builder for method chaining.</returns>
    public WideEventBuilder WithFeature(string featureName, string featureType)
    {
        _event.Feature = new WideEventFeatureSegment
        {
            FeatureName = featureName,
            FeatureType = featureType,
        };
        return this;
    }

    /// <summary>
    /// Adds a message segment to the wide event.
    /// </summary>
    /// <param name="messageId">The message ID.</param>
    /// <param name="messageType">The message type name.</param>
    /// <param name="consumerType">The consumer type name.</param>
    /// <param name="endpointName">The endpoint name.</param>
    /// <returns>This builder for method chaining.</returns>
    public WideEventBuilder WithMessage(string messageId, string messageType, string consumerType, string endpointName)
    {
        _event.Message = new WideEventMessageSegment
        {
            MessageId = messageId,
            MessageType = messageType,
            ConsumerType = consumerType,
            EndpointName = endpointName,
        };
        return this;
    }

    /// <summary>
    /// Sets the correlation ID for the event.
    /// </summary>
    /// <param name="correlationId">The correlation ID.</param>
    /// <returns>This builder for method chaining.</returns>
    public WideEventBuilder WithCorrelationId(string? correlationId)
    {
        _event.CorrelationId = correlationId;
        return this;
    }

    /// <summary>
    /// Sets the conversation and initiator IDs for request-response patterns.
    /// </summary>
    /// <param name="conversationId">The conversation ID.</param>
    /// <param name="initiatorId">The initiator ID.</param>
    /// <returns>This builder for method chaining.</returns>
    public WideEventBuilder WithConversation(string? conversationId, string? initiatorId)
    {
        _event.ConversationId = conversationId;
        _event.InitiatorId = initiatorId;
        return this;
    }

    /// <summary>
    /// Sets the causation ID to link this event to its direct cause.
    /// </summary>
    /// <param name="causationId">The causation ID (typically parent event ID).</param>
    /// <returns>This builder for method chaining.</returns>
    public WideEventBuilder WithCausationId(string? causationId)
    {
        _event.CausationId = causationId;
        return this;
    }

    /// <summary>
    /// Records the request and result types for better queryability.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <returns>This builder for method chaining.</returns>
    public WideEventBuilder WithTypes<TRequest, TResult>()
    {
        if (_event.Feature != null)
        {
            _event.Feature.RequestType = typeof(TRequest).Name;
            _event.Feature.ResultType = typeof(TResult).Name;
        }

        return this;
    }

    /// <summary>
    /// Adds custom context to the wide event (non-sensitive data).
    /// </summary>
    /// <param name="key">The context key.</param>
    /// <param name="value">The context value.</param>
    /// <returns>This builder for method chaining.</returns>
    public WideEventBuilder WithContext(string key, object? value)
    {
        _event.Context[key] = value;
        return this;
    }

    /// <summary>
    /// Extracts context from a strongly typed context object using
    /// <see cref="WideEventPropertyAttribute"/> and <see cref="IWideEventContext"/>.
    /// </summary>
    public WideEventBuilder WithContextObject(object? context)
    {
        foreach (var (key, value) in WideEventContextExtractor.Extract(context))
        {
            _event.Context[key] = value;
        }

        return this;
    }

    /// <summary>
    /// Extracts context from a request object using [WideEventProperty] attributes
    /// and IWideEventContext interface.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <param name="request">The request to extract context from.</param>
    /// <returns>This builder for method chaining.</returns>
    public WideEventBuilder WithRequestContext<TRequest>(TRequest request)
    {
        if (request == null)
        {
            return this;
        }

        return WithContextObject(request);
    }

    /// <summary>
    /// Merges context from a FeatureContext's WideEventContext dictionary and loaded entities.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <param name="context">The feature context to merge from.</param>
    /// <returns>This builder for method chaining.</returns>
    public WideEventBuilder WithFeatureContext<TRequest>(FeatureContext<TRequest>? context)
    {
        if (context == null)
        {
            return this;
        }

        // Merge wide event context
        foreach (var (key, value) in context.WideEventContext)
        {
            _event.Context[key] = value;
        }

        // Capture loaded entities (type names only for non-PII)
        if (_event.Feature != null)
        {
            foreach (var (key, value) in context.Entities)
            {
                _event.Feature.LoadedEntities[key] = value.GetType().Name;
            }
        }

        return this;
    }

    /// <summary>
    /// Merges context from an ErrorOr's Context dictionary.
    /// </summary>
    /// <param name="context">The context dictionary to merge.</param>
    /// <returns>This builder for method chaining.</returns>
    public WideEventBuilder WithResultContext(IReadOnlyDictionary<string, object> context)
    {
        foreach (var (key, value) in context)
        {
            _event.Context[key] = value;
        }

        return this;
    }

    /// <summary>
    /// Records the pipeline stage types for debugging and analytics.
    /// </summary>
    /// <param name="validatorType">The validator type.</param>
    /// <param name="requirementsType">The requirements type.</param>
    /// <param name="mutatorOrQueryType">The mutator or query type.</param>
    /// <param name="sideEffectsType">The side effects type.</param>
    /// <param name="isMutation">Whether this is a mutation feature.</param>
    /// <returns>This builder for method chaining.</returns>
    public WideEventBuilder WithPipelineStages(
        Type? validatorType,
        Type? requirementsType,
        Type? mutatorOrQueryType,
        Type? sideEffectsType,
        bool isMutation)
    {
        if (_event.Feature == null)
        {
            return this;
        }

        if (validatorType != null)
        {
            _event.Feature.ValidatorType = validatorType.Name;
            _event.Feature.HasCustomValidator = !IsNoOpType(validatorType, typeof(NoOpValidator<>));
        }

        if (requirementsType != null)
        {
            _event.Feature.RequirementsType = requirementsType.Name;
            _event.Feature.HasCustomRequirements = !IsNoOpType(requirementsType, typeof(NoOpRequirements<>));
        }

        if (mutatorOrQueryType != null)
        {
            if (isMutation)
            {
                _event.Feature.MutatorType = mutatorOrQueryType.Name;
            }
            else
            {
                _event.Feature.QueryType = mutatorOrQueryType.Name;
            }
        }

        if (sideEffectsType != null)
        {
            _event.Feature.SideEffectsType = sideEffectsType.Name;
            _event.Feature.HasCustomSideEffects = !IsNoOpType(sideEffectsType, typeof(NoOpSideEffects<>));
        }

        return this;
    }

    /// <summary>
    /// Configures message segment with envelope information.
    /// </summary>
    /// <param name="sourceAddress">The source address.</param>
    /// <param name="destinationAddress">The destination address.</param>
    /// <param name="responseAddress">The response address.</param>
    /// <param name="faultAddress">The fault address.</param>
    /// <param name="messageTypes">The full message type hierarchy.</param>
    /// <returns>This builder for method chaining.</returns>
    public WideEventBuilder WithMessageAddresses(
        string? sourceAddress,
        string? destinationAddress,
        string? responseAddress,
        string? faultAddress,
        string[]? messageTypes)
    {
        if (_event.Message == null)
        {
            return this;
        }

        _event.Message.SourceAddress = sourceAddress;
        _event.Message.DestinationAddress = destinationAddress;
        _event.Message.ResponseAddress = responseAddress;
        _event.Message.FaultAddress = faultAddress;
        _event.Message.MessageTypes = messageTypes;
        return this;
    }

    /// <summary>
    /// Records retry information for message processing.
    /// </summary>
    /// <param name="attempt">The current retry attempt.</param>
    /// <param name="maxRetries">The maximum retry count.</param>
    /// <param name="redelivered">Whether the message was redelivered.</param>
    /// <returns>This builder for method chaining.</returns>
    public WideEventBuilder WithRetryInfo(int attempt, int? maxRetries, bool redelivered)
    {
        if (_event.Message == null)
        {
            return this;
        }

        _event.Message.RetryAttempt = attempt;
        _event.Message.MaxRetryCount = maxRetries;
        _event.Message.Redelivered = redelivered;
        return this;
    }

    /// <summary>
    /// Records filters applied during message processing.
    /// </summary>
    /// <param name="filterTypes">The filter type names.</param>
    /// <returns>This builder for method chaining.</returns>
    public WideEventBuilder WithFilters(string[]? filterTypes)
    {
        if (_event.Message != null)
        {
            _event.Message.FilterTypes = filterTypes;
        }

        return this;
    }

    /// <summary>
    /// Records the queue time for the message.
    /// </summary>
    /// <param name="queueTimeMs">Time the message spent in the queue.</param>
    /// <returns>This builder for method chaining.</returns>
    public WideEventBuilder WithQueueTime(double? queueTimeMs)
    {
        if (_event.Message != null)
        {
            _event.Message.QueueTimeMs = queueTimeMs;
        }

        return this;
    }

    /// <summary>
    /// Adds a child span to the event.
    /// </summary>
    /// <param name="childSpan">The child span to add.</param>
    /// <returns>This builder for method chaining.</returns>
    public WideEventBuilder AddChildSpan(WideEventChildSpan childSpan)
    {
        _event.ChildSpans.Add(childSpan);
        return this;
    }

    // Stage timing methods

    /// <summary>
    /// Starts timing a stage of the operation.
    /// </summary>
    public void StartStage() => _stageStopwatch.Restart();

    /// <summary>
    /// Starts timing a stage and records the stage name for exception tracking.
    /// </summary>
    /// <param name="stageName">The name of the stage being executed.</param>
    public void StartStage(string stageName)
    {
        _currentStage = stageName;
        _currentNamespace = null;
        _currentClass = null;
        _currentMethod = null;
        _stageStopwatch.Restart();
    }

    /// <summary>
    /// Starts timing a stage and records the component type being executed.
    /// </summary>
    /// <param name="stageName">The name of the stage being executed.</param>
    /// <param name="componentType">The type of the component being executed.</param>
    /// <param name="methodName">The method name being called on the component.</param>
    public void StartStage(string stageName, Type? componentType, string methodName)
    {
        _currentStage = stageName;
        _currentNamespace = componentType?.Namespace;
        _currentClass = componentType?.Name;
        _currentMethod = methodName;
        _stageStopwatch.Restart();
    }

    /// <summary>
    /// Records the duration of the binding stage.
    /// Stores timing in the event context as "binding_ms".
    /// </summary>
    public void RecordBinding()
    {
        _event.Context["binding_ms"] = _stageStopwatch.Elapsed.TotalMilliseconds;
        _stageStopwatch.Reset();
    }

    /// <summary>
    /// Records the duration of the validation stage.
    /// </summary>
    public void RecordValidation()
    {
        if (_event.Feature != null)
        {
            _event.Feature.ValidationMs = _stageStopwatch.Elapsed.TotalMilliseconds;
        }

        _stageStopwatch.Reset();
    }

    /// <summary>
    /// Records the duration of the requirements stage.
    /// </summary>
    public void RecordRequirements()
    {
        if (_event.Feature != null)
        {
            _event.Feature.RequirementsMs = _stageStopwatch.Elapsed.TotalMilliseconds;
        }

        _stageStopwatch.Reset();
    }

    /// <summary>
    /// Records the duration of the execution stage.
    /// </summary>
    public void RecordExecution()
    {
        if (_event.Feature != null)
        {
            _event.Feature.ExecutionMs = _stageStopwatch.Elapsed.TotalMilliseconds;
        }

        _stageStopwatch.Reset();
    }

    /// <summary>
    /// Records the duration of the side effects stage.
    /// </summary>
    public void RecordSideEffects()
    {
        if (_event.Feature != null)
        {
            _event.Feature.SideEffectsMs = _stageStopwatch.Elapsed.TotalMilliseconds;
        }

        _stageStopwatch.Reset();
    }

    /// <summary>
    /// Records the duration of message deserialization.
    /// </summary>
    public void RecordDeserialization()
    {
        if (_event.Message != null)
        {
            _event.Message.DeserializationMs = _stageStopwatch.Elapsed.TotalMilliseconds;
        }

        _stageStopwatch.Reset();
    }

    /// <summary>
    /// Records the duration of pre-consume filters.
    /// </summary>
    public void RecordPreConsumeFilters()
    {
        if (_event.Message != null)
        {
            _event.Message.PreConsumeFiltersMs = _stageStopwatch.Elapsed.TotalMilliseconds;
        }

        _stageStopwatch.Reset();
    }

    /// <summary>
    /// Records the duration of consumer execution.
    /// </summary>
    public void RecordConsumer()
    {
        if (_event.Message != null)
        {
            _event.Message.ConsumerMs = _stageStopwatch.Elapsed.TotalMilliseconds;
        }

        _stageStopwatch.Reset();
    }

    /// <summary>
    /// Records the duration of post-consume filters.
    /// </summary>
    public void RecordPostConsumeFilters()
    {
        if (_event.Message != null)
        {
            _event.Message.PostConsumeFiltersMs = _stageStopwatch.Elapsed.TotalMilliseconds;
        }

        _stageStopwatch.Reset();
    }

    // Outcome methods

    /// <summary>
    /// Marks the operation as successful.
    /// </summary>
    /// <returns>The completed wide event.</returns>
    public WideEvent Success()
    {
        _event.Outcome = "success";
        _event.Error = null;
        return Build();
    }

    /// <summary>
    /// Marks the operation as failed due to request binding.
    /// </summary>
    /// <param name="errors">The binding errors.</param>
    /// <returns>The completed wide event.</returns>
    public WideEvent BindingFailure(IReadOnlyList<Error> errors)
    {
        _event.Outcome = "binding_failure";
        _event.Error = WideEventErrorSegment.FromErrors(errors, "binding");
        PopulateFailureLocation();
        return Build();
    }

    /// <summary>
    /// Marks the operation as failed due to validation.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    /// <returns>The completed wide event.</returns>
    public WideEvent ValidationFailure(IReadOnlyList<Error> errors)
    {
        _event.Outcome = "validation_failure";
        _event.Error = WideEventErrorSegment.FromErrors(errors, "validation");
        PopulateFailureLocation();
        return Build();
    }

    /// <summary>
    /// Marks the operation as failed due to requirements enforcement.
    /// </summary>
    /// <param name="errors">The requirements errors.</param>
    /// <returns>The completed wide event.</returns>
    public WideEvent RequirementsFailure(IReadOnlyList<Error> errors)
    {
        _event.Outcome = "requirements_failure";
        _event.Error = WideEventErrorSegment.FromErrors(errors, "requirements");
        PopulateFailureLocation();
        return Build();
    }

    /// <summary>
    /// Marks the operation as failed during execution.
    /// </summary>
    /// <param name="errors">The execution errors.</param>
    /// <returns>The completed wide event.</returns>
    public WideEvent ExecutionFailure(IReadOnlyList<Error> errors)
    {
        _event.Outcome = "execution_failure";
        _event.Error = WideEventErrorSegment.FromErrors(errors, "execution");
        PopulateFailureLocation();
        return Build();
    }

    /// <summary>
    /// Marks the operation as failed during side effects.
    /// </summary>
    /// <param name="errors">The side effects errors.</param>
    /// <returns>The completed wide event.</returns>
    public WideEvent SideEffectsFailure(IReadOnlyList<Error> errors)
    {
        _event.Outcome = "side_effects_failure";
        _event.Error = WideEventErrorSegment.FromErrors(errors, "side_effects");
        PopulateFailureLocation();
        return Build();
    }

    /// <summary>
    /// Marks the message processing as failed due to consumer error.
    /// </summary>
    /// <param name="errors">The consumer errors.</param>
    /// <returns>The completed wide event.</returns>
    public WideEvent ConsumerError(IReadOnlyList<Error> errors)
    {
        _event.Outcome = "consumer_error";
        _event.Error = WideEventErrorSegment.FromErrors(errors, "consumer");
        PopulateFailureLocation();
        return Build();
    }

    /// <summary>
    /// Marks the message processing as failed due to deserialization error.
    /// </summary>
    /// <param name="errors">The deserialization errors.</param>
    /// <returns>The completed wide event.</returns>
    public WideEvent DeserializationError(IReadOnlyList<Error> errors)
    {
        _event.Outcome = "deserialization_error";
        _event.Error = WideEventErrorSegment.FromErrors(errors, "deserialization");
        return Build();
    }

    /// <summary>
    /// Marks the message processing as failed after exhausting retries.
    /// </summary>
    /// <param name="errors">The errors that caused retry exhaustion.</param>
    /// <returns>The completed wide event.</returns>
    public WideEvent RetryExhausted(IReadOnlyList<Error> errors)
    {
        _event.Outcome = "retry_exhausted";
        _event.Error = WideEventErrorSegment.FromErrors(errors, "retry");
        return Build();
    }

    /// <summary>
    /// Marks the message processing as failed due to circuit breaker.
    /// </summary>
    /// <returns>The completed wide event.</returns>
    public WideEvent CircuitBreakerOpen()
    {
        _event.Outcome = "circuit_breaker_open";
        _event.Error = new WideEventErrorSegment { FailedAtStage = "circuit_breaker", Count = 0 };
        return Build();
    }

    /// <summary>
    /// Marks the message processing as timed out.
    /// </summary>
    /// <param name="timeoutMs">The timeout duration in milliseconds.</param>
    /// <returns>The completed wide event.</returns>
    public WideEvent Timeout(double timeoutMs)
    {
        _event.Outcome = "timeout";
        _event.Error = new WideEventErrorSegment
        {
            FailedAtStage = "timeout",
            Message = $"Operation timed out after {timeoutMs:F0}ms",
            Count = 0,
        };
        return Build();
    }

    /// <summary>
    /// Marks the operation as failed due to an unhandled exception.
    /// </summary>
    /// <param name="ex">The exception that occurred.</param>
    /// <param name="includeStackTrace">Whether to include the stack trace.</param>
    /// <returns>The completed wide event.</returns>
    public WideEvent Exception(Exception ex, bool includeStackTrace = false)
    {
        _event.Outcome = "exception";
        _event.Error = WideEventErrorSegment.FromException(ex, _currentStage, includeStackTrace);
        PopulateFailureLocation();
        return Build();
    }

    /// <summary>
    /// Records that a fault message was published.
    /// </summary>
    /// <param name="faultMessageId">The fault message ID.</param>
    /// <returns>This builder for method chaining.</returns>
    public WideEventBuilder WithFaultPublished(string? faultMessageId)
    {
        if (_event.Message != null)
        {
            _event.Message.FaultPublished = true;
            _event.Message.FaultMessageId = faultMessageId;
        }

        return this;
    }

    private static bool IsNoOpType(Type type, Type noOpOpenGeneric)
        => type.IsGenericType && type.GetGenericTypeDefinition() == noOpOpenGeneric;

    private void PopulateFailureLocation()
    {
        if (_event.Error == null)
        {
            return;
        }

        _event.Error.FailedInNamespace = _currentNamespace;
        _event.Error.FailedInClass = _currentClass;
        _event.Error.FailedInMethod = _currentMethod;
    }

    private WideEvent Build()
    {
        _totalStopwatch.Stop();
        _event.TotalMs = _totalStopwatch.Elapsed.TotalMilliseconds;
        return _event.Clone();
    }

    private sealed class EnvironmentInfo
    {
        public string? ServiceName { get; init; }
        public string? ServiceVersion { get; init; }
        public string? CommitHash { get; init; }
        public string? Environment { get; init; }
        public string? DeploymentId { get; init; }
        public string? Region { get; init; }
        public string? Host { get; init; }
    }
}
