namespace PolyStore.Core;

public sealed class SourceRelation<T>(string name)
    : Relation<T>(name, new SourceExpression(name, typeof(T))), ISource<T>
{
    /// <inheritdoc />
    public ValueTask InsertAsync(T value, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public ValueTask UpdateAsync(T value, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public ValueTask DeleteAsync(T value, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}