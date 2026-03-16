using VsaResults.Errors;

namespace VsaResults.VsaResult;

/// <summary>
/// An equality comparer for <see cref="VsaResult{TValue}"/> that compares errors without considering their order.
/// </summary>
/// <typeparam name="TValue">The type of the value in the ErrorOr.</typeparam>
/// <remarks>
/// The default equality comparison for ErrorOr requires errors to be in the same order.
/// This comparer allows comparing ErrorOr instances where the same errors may appear in different orders.
/// </remarks>
/// <example>
/// <code>
/// var comparer = VsaResultUnorderedEqualityComparer&lt;int&gt;.Instance;
/// var result1 = ErrorOr&lt;int&gt;.From([Error.Validation("A"), Error.Validation("B")]);
/// var result2 = ErrorOr&lt;int&gt;.From([Error.Validation("B"), Error.Validation("A")]);
/// var areEqual = comparer.Equals(result1, result2); // true
/// </code>
/// </example>
public sealed class VsaResultUnorderedEqualityComparer<TValue> : IEqualityComparer<VsaResult<TValue>>
{
    private VsaResultUnorderedEqualityComparer()
    {
    }

    /// <summary>
    /// Gets the singleton instance of the comparer.
    /// </summary>
    public static VsaResultUnorderedEqualityComparer<TValue> Instance { get; } = new();

    /// <summary>
    /// Determines whether two ErrorOr instances are equal, ignoring the order of errors.
    /// </summary>
    /// <param name="x">The first ErrorOr to compare.</param>
    /// <param name="y">The second ErrorOr to compare.</param>
    /// <returns>true if the ErrorOr instances are equal; otherwise, false.</returns>
    public bool Equals(VsaResult<TValue> x, VsaResult<TValue> y)
    {
        // If one is error and the other is not, they're not equal
        if (x.IsError != y.IsError)
        {
            return false;
        }

        // If both are successful, compare values
        if (!x.IsError)
        {
            return EqualityComparer<TValue>.Default.Equals(x.Value, y.Value);
        }

        // Both are errors - compare unordered
        var xErrors = x.Errors;
        var yErrors = y.Errors;

        if (xErrors.Count != yErrors.Count)
        {
            return false;
        }

        // Use a dictionary to count occurrences
        var errorCounts = new Dictionary<Error, int>(xErrors.Count);

        foreach (var error in xErrors)
        {
            errorCounts.TryGetValue(error, out var count);
            errorCounts[error] = count + 1;
        }

        foreach (var error in yErrors)
        {
            if (!errorCounts.TryGetValue(error, out var count) || count == 0)
            {
                return false;
            }

            errorCounts[error] = count - 1;
        }

        return true;
    }

    /// <summary>
    /// Returns a hash code for the specified ErrorOr instance.
    /// </summary>
    /// <param name="obj">The ErrorOr for which to get a hash code.</param>
    /// <returns>A hash code for the ErrorOr.</returns>
    /// <remarks>
    /// The hash code is order-independent for errors, using XOR to combine error hash codes.
    /// </remarks>
    public int GetHashCode(VsaResult<TValue> obj)
    {
        if (!obj.IsError)
        {
            return obj.Value?.GetHashCode() ?? 0;
        }

        // Use XOR for order-independent hashing
        var hash = 0;
        foreach (var error in obj.Errors)
        {
            hash ^= error.GetHashCode();
        }

        return hash;
    }
}
