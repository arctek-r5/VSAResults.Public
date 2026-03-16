using System.Diagnostics;
using VsaResults.Errors;
using VsaResults.Messaging.Consumers;
using VsaResults.Messaging.Messages;
using VsaResults.Messaging.Transports;

namespace VsaResults.Messaging.WideEvents;

/// <summary>
/// Builder for constructing wide events throughout the message processing lifecycle.
/// </summary>
public sealed class MessageWideEventBuilder
{
    private readonly MessageWideEvent _event;
    private readonly Stopwatch _totalStopwatch;
    private readonly Stopwatch _stageStopwatch;

    internal MessageWideEventBuilder(
        string messageId,
        string correlationId,
        string messageType,
        string consumerType,
        string endpointName)
    {
        _totalStopwatch = Stopwatch.StartNew();
        _stageStopwatch = new Stopwatch();

        var activity = Activity.Current;

        _event = new MessageWideEvent
        {
            MessageId = messageId,
            CorrelationId = correlationId,
            MessageType = messageType,
            ConsumerType = consumerType,
            EndpointName = endpointName,
            Outcome = "pending",
            TraceId = activity?.TraceId.ToString(),
            SpanId = activity?.SpanId.ToString(),
            ParentSpanId = activity?.ParentSpanId.ToString(),
            ServiceName = Environment.GetEnvironmentVariable("SERVICE_NAME"),
            ServiceVersion = Environment.GetEnvironmentVariable("SERVICE_VERSION"),
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT"),
            DeploymentId = Environment.GetEnvironmentVariable("DEPLOYMENT_ID"),
            Region = Environment.GetEnvironmentVariable("AZURE_REGION")
                ?? Environment.GetEnvironmentVariable("AWS_REGION"),
            Host = Environment.MachineName,
        };
    }

    /// <summary>
    /// Records the full message type hierarchy.
    /// </summary>
    /// <param name="messageTypes">The message types.</param>
    /// <returns>This builder for method chaining.</returns>
    public MessageWideEventBuilder WithMessageTypes(string[] messageTypes)
    {
        _event.MessageTypes = messageTypes;
        return this;
    }

    /// <summary>
    /// Records the message envelope information.
    /// </summary>
    /// <param name="envelope">The message envelope.</param>
    /// <returns>This builder for method chaining.</returns>
    public MessageWideEventBuilder WithEnvelope(MessageEnvelope envelope)
    {
        _event.ConversationId = envelope.ConversationId?.ToString();
        _event.InitiatorId = envelope.InitiatorId?.ToString();
        _event.SourceAddress = envelope.SourceAddress?.ToString();
        _event.DestinationAddress = envelope.DestinationAddress?.ToString();
        _event.ResponseAddress = envelope.ResponseAddress?.ToString();
        _event.FaultAddress = envelope.FaultAddress?.ToString();

        // Calculate queue time from sent time
        _event.QueueTimeMs = (DateTimeOffset.UtcNow - envelope.SentTime).TotalMilliseconds;

        // Copy headers to message context
        foreach (var (key, value) in envelope.Headers)
        {
            _event.MessageContext[$"header_{key}"] = value;
        }

        return this;
    }

    /// <summary>
    /// Records the endpoint information.
    /// </summary>
    /// <param name="inputAddress">The input address.</param>
    /// <returns>This builder for method chaining.</returns>
    public MessageWideEventBuilder WithEndpoint(EndpointAddress inputAddress)
    {
        _event.InputAddress = inputAddress.ToString();
        return this;
    }

    /// <summary>
    /// Records retry information.
    /// </summary>
    /// <param name="attempt">The current retry attempt.</param>
    /// <param name="maxRetries">The maximum retry count.</param>
    /// <param name="redelivered">Whether the message was redelivered.</param>
    /// <returns>This builder for method chaining.</returns>
    public MessageWideEventBuilder WithRetryInfo(int attempt, int? maxRetries = null, bool redelivered = false)
    {
        _event.RetryAttempt = attempt;
        _event.MaxRetryCount = maxRetries;
        _event.Redelivered = redelivered;
        return this;
    }

    /// <summary>
    /// Records the filters applied during processing.
    /// </summary>
    /// <param name="filterTypes">The filter type names.</param>
    /// <returns>This builder for method chaining.</returns>
    public MessageWideEventBuilder WithFilters(params string[] filterTypes)
    {
        _event.FilterTypes = filterTypes;
        return this;
    }

    /// <summary>
    /// Adds custom context to the wide event.
    /// </summary>
    /// <param name="key">The context key.</param>
    /// <param name="value">The context value.</param>
    /// <returns>This builder for method chaining.</returns>
    public MessageWideEventBuilder WithContext(string key, object? value)
    {
        _event.MessageContext[key] = value;
        return this;
    }

    /// <summary>
    /// Merges context from a ConsumeContext's WideEventContext dictionary.
    /// </summary>
    /// <param name="context">The consume context to merge from.</param>
    /// <returns>This builder for method chaining.</returns>
    public MessageWideEventBuilder WithConsumeContext(ConsumeContext context)
    {
        foreach (var (key, value) in context.WideEventContext)
        {
            _event.MessageContext[key] = value;
        }

        return this;
    }

    /// <summary>
    /// Starts timing a stage of message processing.
    /// </summary>
    public void StartStage() => _stageStopwatch.Restart();

    /// <summary>
    /// Records the duration of message deserialization.
    /// </summary>
    public void RecordDeserialization()
    {
        _event.DeserializationMs = _stageStopwatch.Elapsed.TotalMilliseconds;
        _stageStopwatch.Reset();
    }

    /// <summary>
    /// Records the duration of pre-consume filter execution.
    /// </summary>
    public void RecordPreConsumeFilters()
    {
        _event.PreConsumeFiltersMs = _stageStopwatch.Elapsed.TotalMilliseconds;
        _stageStopwatch.Reset();
    }

    /// <summary>
    /// Records the duration of consumer execution.
    /// </summary>
    public void RecordConsumer()
    {
        _event.ConsumerMs = _stageStopwatch.Elapsed.TotalMilliseconds;
        _stageStopwatch.Reset();
    }

    /// <summary>
    /// Records the duration of post-consume filter execution.
    /// </summary>
    public void RecordPostConsumeFilters()
    {
        _event.PostConsumeFiltersMs = _stageStopwatch.Elapsed.TotalMilliseconds;
        _stageStopwatch.Reset();
    }

    /// <summary>
    /// Marks the message processing as successful.
    /// </summary>
    /// <returns>The completed wide event.</returns>
    public MessageWideEvent Success()
    {
        _event.Outcome = "success";
        return Build();
    }

    /// <summary>
    /// Marks the message processing as failed due to consumer returning errors.
    /// </summary>
    /// <param name="errors">The errors from the consumer.</param>
    /// <returns>The completed wide event.</returns>
    public MessageWideEvent ConsumerError(IReadOnlyList<Error> errors)
    {
        _event.Outcome = "consumer_error";
        _event.FailedAtStage = "consumer";
        PopulateErrors(errors);
        return Build();
    }

    /// <summary>
    /// Marks the message processing as failed due to deserialization error.
    /// </summary>
    /// <param name="errors">The deserialization errors.</param>
    /// <returns>The completed wide event.</returns>
    public MessageWideEvent DeserializationError(IReadOnlyList<Error> errors)
    {
        _event.Outcome = "deserialization_error";
        _event.FailedAtStage = "deserialization";
        PopulateErrors(errors);
        return Build();
    }

    /// <summary>
    /// Marks the message processing as failed due to retry exhaustion.
    /// </summary>
    /// <param name="lastErrors">The errors from the last attempt.</param>
    /// <returns>The completed wide event.</returns>
    public MessageWideEvent RetryExhausted(IReadOnlyList<Error> lastErrors)
    {
        _event.Outcome = "retry_exhausted";
        _event.FailedAtStage = "retry";
        PopulateErrors(lastErrors);
        return Build();
    }

    /// <summary>
    /// Marks the message processing as failed due to circuit breaker being open.
    /// </summary>
    /// <returns>The completed wide event.</returns>
    public MessageWideEvent CircuitBreakerOpen()
    {
        _event.Outcome = "circuit_breaker_open";
        _event.FailedAtStage = "circuit_breaker";
        _event.ErrorCode = "CircuitBreaker.Open";
        _event.ErrorMessage = "Circuit breaker is open, message not processed";
        return Build();
    }

    /// <summary>
    /// Marks the message processing as failed due to timeout.
    /// </summary>
    /// <param name="timeoutMs">The timeout duration in milliseconds.</param>
    /// <returns>The completed wide event.</returns>
    public MessageWideEvent Timeout(double timeoutMs)
    {
        _event.Outcome = "timeout";
        _event.FailedAtStage = "timeout";
        _event.ErrorCode = "Timeout";
        _event.ErrorMessage = $"Message processing timed out after {timeoutMs}ms";
        return Build();
    }

    /// <summary>
    /// Marks the message processing as failed due to an unhandled exception.
    /// </summary>
    /// <param name="ex">The exception that occurred.</param>
    /// <returns>The completed wide event.</returns>
    public MessageWideEvent Exception(Exception ex)
    {
        _event.Outcome = "exception";
        _event.FailedAtStage = "unknown";
        _event.ExceptionType = ex.GetType().FullName;
        _event.ExceptionMessage = ex.Message;
        _event.ExceptionStackTrace = ex.StackTrace;
        return Build();
    }

    /// <summary>
    /// Records that a fault message was published.
    /// </summary>
    /// <param name="faultMessageId">The fault message ID.</param>
    /// <returns>This builder for method chaining.</returns>
    public MessageWideEventBuilder WithFaultPublished(MessageId faultMessageId)
    {
        _event.FaultPublished = true;
        _event.FaultMessageId = faultMessageId.ToString();
        return this;
    }

    private void PopulateErrors(IReadOnlyList<Error> errors)
    {
        if (errors.Count == 0)
        {
            return;
        }

        var firstError = errors[0];
        _event.ErrorCode = firstError.Code;
        _event.ErrorType = firstError.Type.ToString();
        _event.ErrorMessage = firstError.Description;
        _event.ErrorCount = errors.Count;

        if (errors.Count > 1)
        {
            _event.ErrorDescription = string.Join("; ", errors.Select(e => $"{e.Code}: {e.Description}"));
        }
    }

    private MessageWideEvent Build()
    {
        _totalStopwatch.Stop();
        _event.TotalMs = _totalStopwatch.Elapsed.TotalMilliseconds;
        return _event;
    }
}
