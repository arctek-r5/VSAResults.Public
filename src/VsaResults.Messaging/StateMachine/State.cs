namespace VsaResults.Messaging.StateMachine;

/// <summary>
/// Represents a state in a state machine.
/// </summary>
public sealed class State
{
    private State(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Gets the name of the state.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets a value indicating whether this is an initial state.
    /// </summary>
    public bool IsInitial => Name == "Initial";

    /// <summary>
    /// Gets a value indicating whether this is a final state.
    /// </summary>
    public bool IsFinal => Name == "Final";

    /// <summary>
    /// The initial state for all state machines.
    /// </summary>
    public static readonly State Initial = new("Initial");

    /// <summary>
    /// The final (completed) state for all state machines.
    /// </summary>
    public static readonly State Final = new("Final");

    /// <summary>
    /// The faulted state for state machines that support retry.
    /// </summary>
    public static readonly State Faulted = new("Faulted");

    /// <summary>
    /// Creates a new state with the specified name.
    /// </summary>
    /// <param name="name">The state name.</param>
    /// <returns>A new state instance.</returns>
    public static State Create(string name) => new(name);

    /// <inheritdoc />
    public override string ToString() => Name;

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is State other && Name == other.Name;

    /// <inheritdoc />
    public override int GetHashCode() => Name.GetHashCode();

    /// <summary>
    /// Equality operator for states.
    /// </summary>
    public static bool operator ==(State? left, State? right) =>
        left?.Name == right?.Name;

    /// <summary>
    /// Inequality operator for states.
    /// </summary>
    public static bool operator !=(State? left, State? right) =>
        !(left == right);
}
