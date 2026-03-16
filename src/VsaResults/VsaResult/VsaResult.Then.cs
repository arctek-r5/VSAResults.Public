namespace VsaResults.VsaResult;

public readonly partial record struct VsaResult<TValue> : IVsaResult<TValue>
{
    /// <summary>
    /// If the state is a value, the provided function <paramref name="onValue"/> is executed and its result is returned.
    /// Context is propagated to the result.
    /// </summary>
    /// <typeparam name="TNextValue">The type of the result.</typeparam>
    /// <param name="onValue">The function to execute if the state is a value.</param>
    /// <returns>The result from calling <paramref name="onValue"/> if state is value; otherwise the original <see cref="Errors"/>.</returns>
    public VsaResult<TNextValue> Then<TNextValue>(Func<TValue, VsaResult<TNextValue>> onValue) =>
        IsError
            ? new VsaResult<TNextValue>(_errors, _context)
            : onValue(Value).MergeContext(_context);

    /// <summary>
    /// If the state is a value, the provided <paramref name="action"/> is invoked.
    /// Context is preserved.
    /// </summary>
    /// <param name="action">The action to execute if the state is a value.</param>
    /// <returns>The original <see cref="ErrorOr"/> instance with context preserved.</returns>
    public VsaResult<TValue> ThenDo(Action<TValue> action)
    {
        if (!IsError)
        {
            action(Value);
        }

        return this;
    }

    /// <summary>
    /// If the state is a value, the provided function <paramref name="onValue"/> is executed and its result is returned.
    /// Context is propagated to the result.
    /// </summary>
    /// <typeparam name="TNextValue">The type of the result.</typeparam>
    /// <param name="onValue">The function to execute if the state is a value.</param>
    /// <returns>The result from calling <paramref name="onValue"/> if state is value; otherwise the original <see cref="Errors"/>.</returns>
    public VsaResult<TNextValue> Then<TNextValue>(Func<TValue, TNextValue> onValue) =>
        IsError
            ? new VsaResult<TNextValue>(_errors, _context)
            : new VsaResult<TNextValue>(onValue(Value), _context);

    /// <summary>
    /// If the state is a value, the provided function <paramref name="onValue"/> is executed asynchronously and its result is returned.
    /// Context is propagated to the result.
    /// </summary>
    /// <typeparam name="TNextValue">The type of the result.</typeparam>
    /// <param name="onValue">The function to execute if the state is a value.</param>
    /// <returns>The result from calling <paramref name="onValue"/> if state is value; otherwise the original <see cref="Errors"/>.</returns>
    public Task<VsaResult<TNextValue>> ThenAsync<TNextValue>(Func<TValue, Task<VsaResult<TNextValue>>> onValue)
    {
        if (IsError)
        {
            return Task.FromResult(new VsaResult<TNextValue>(_errors, _context));
        }

        var context = _context;
        return onValue(Value).ContinueWith(t => t.GetAwaiter().GetResult().MergeContext(context), TaskContinuationOptions.ExecuteSynchronously);
    }

    /// <summary>
    /// If the state is a value, the provided <paramref name="action"/> is invoked asynchronously.
    /// Context is preserved.
    /// </summary>
    /// <param name="action">The action to execute if the state is a value.</param>
    /// <returns>The original <see cref="ErrorOr"/> instance with context preserved.</returns>
    public Task<VsaResult<TValue>> ThenDoAsync(Func<TValue, Task> action)
    {
        if (IsError)
        {
            return Task.FromResult(this);
        }

        var self = this;
        return action(Value).ContinueWith(_ => self, TaskContinuationOptions.ExecuteSynchronously);
    }

    /// <summary>
    /// If the state is a value, the provided function <paramref name="onValue"/> is executed asynchronously and its result is returned.
    /// Context is propagated to the result.
    /// </summary>
    /// <typeparam name="TNextValue">The type of the result.</typeparam>
    /// <param name="onValue">The function to execute if the state is a value.</param>
    /// <returns>The result from calling <paramref name="onValue"/> if state is value; otherwise the original <see cref="Errors"/>.</returns>
    public Task<VsaResult<TNextValue>> ThenAsync<TNextValue>(Func<TValue, Task<TNextValue>> onValue)
    {
        if (IsError)
        {
            return Task.FromResult(new VsaResult<TNextValue>(_errors, _context));
        }

        var context = _context;
        return onValue(Value).ContinueWith(t => new VsaResult<TNextValue>(t.GetAwaiter().GetResult(), context), TaskContinuationOptions.ExecuteSynchronously);
    }
}
