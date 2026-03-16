namespace VsaResults.Features.Features;

/// <summary>
/// Represents the absence of a value (void equivalent for ErrorOr operations).
/// Used as the result type for side effects that don't return a value.
/// </summary>
public readonly struct Unit : IEquatable<Unit>
{
    /// <summary>
    /// Gets the singleton instance of Unit.
    /// </summary>
    // ReSharper disable once UnassignedReadonlyField — Unit is a zero-size struct; default initialization is intentional
    public static readonly Unit Value;

    /// <summary>
    /// Equality operator.
    /// </summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns>Always true, as all Unit values are equal.</returns>
    public static bool operator ==(Unit left, Unit right) => true;

    /// <summary>
    /// Inequality operator.
    /// </summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns>Always false, as all Unit values are equal.</returns>
    public static bool operator !=(Unit left, Unit right) => false;

    /// <summary>
    /// Returns true, as all Unit values are equal.
    /// </summary>
    /// <param name="other">The other Unit to compare.</param>
    /// <returns>Always true.</returns>
    public bool Equals(Unit other) => true;

    /// <summary>
    /// Returns true if the other object is a Unit.
    /// </summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns>True if the object is a Unit.</returns>
    public override bool Equals(object? obj) => obj is Unit;

    /// <summary>
    /// Returns a constant hash code.
    /// </summary>
    /// <returns>Zero, as all Unit values are equal.</returns>
    public override int GetHashCode() => 0;

    /// <summary>
    /// Returns a string representation of Unit.
    /// </summary>
    /// <returns>The string "()".</returns>
    public override string ToString() => "()";
}
