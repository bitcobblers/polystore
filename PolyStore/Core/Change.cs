namespace PolyStore.Core;

/// <summary>
/// Defines a single changed record.
/// </summary>
/// <param name="OldValue">The old record value.</param>
/// <param name="NewValue">The new record value.</param>
/// <typeparam name="T">The record type.</typeparam>
public record Change<T>(T OldValue, T NewValue);
