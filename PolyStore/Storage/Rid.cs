using System;

namespace PolyStore.Storage;

/// <summary>
/// Represents a logical tuple identifier (RID).
/// </summary>
/// <remarks>
/// A RID identifies a tuple within the canonical store. It is a logical
/// identifier, not a physical memory or disk address. Consumers should not
/// depend on a RID representing a stable physical location.
/// The precise RID representation is not yet finalized; this implementation
/// uses a simple non-negative integer that can be extended in the future.
/// </remarks>
public readonly struct Rid : IEquatable<Rid>
{
    /// <summary>
    /// Gets the numeric value of this RID.
    /// </summary>
    public ulong Value { get; }

    /// <summary>
    /// Creates a new RID.
    /// </summary>
    /// <param name="value">The numeric value.</param>
    public Rid(ulong value)
    {
        Value = value;
    }

    /// <inheritdoc />
    public bool Equals(Rid other) => Value == other.Value;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Rid other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();

    /// <summary>
    /// Determines whether two RIDs are equal.
    /// </summary>
    public static bool operator ==(Rid left, Rid right) => left.Value == right.Value;

    /// <summary>
    /// Determines whether two RIDs are not equal.
    /// </summary>
    public static bool operator !=(Rid left, Rid right) => left.Value != right.Value;

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
