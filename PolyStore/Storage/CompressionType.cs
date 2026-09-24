namespace PolyStore.Storage;

/// <summary>
/// Defines the supported compression algorithms.
/// </summary>
public enum CompressionType
{
    /// <summary>
    /// No compression is applied; data is stored as-is.
    /// </summary>
    None,

    /// <summary>
    /// Brotli compression (RFC 7932).
    /// </summary>
    Brotli,

    /// <summary>
    /// Gzip compression (RFC 1952 / RFC 1951).
    /// </summary>
    Gzip,

    /// <summary>
    /// Zstandard (zstd) compression.
    /// </summary>
    Zstd
};
