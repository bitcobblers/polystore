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
public readonly struct Rid() : IEquatable<Rid>
{
    private readonly Guid _value = Guid.CreateVersion7();

    /// <inheritdoc />
    public bool Equals(Rid other) => _value == other._value;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Rid other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _value.GetHashCode();

    /// <summary>
    /// Determines whether two RIDs are equal.
    /// </summary>
    public static bool operator ==(Rid left, Rid right) => left._value == right._value;

    /// <summary>
    /// Determines whether two RIDs are not equal.
    /// </summary>
    public static bool operator !=(Rid left, Rid right) => left._value != right._value;

    /// <inheritdoc />
    public override string ToString() => _value.ToString("N");
}
