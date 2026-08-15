namespace PolyStore.Storage;

[Flags]
public enum RelationAccessorCapabilities 
{
    None = 0,
    Scan = 0x01,
    Seek = 0x02,
    Ordered = 0x04,
    Durable = 0x08,
    Writable = 0x10,
    Columnar = 0x20
}