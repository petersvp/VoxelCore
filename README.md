# VoxelCore

VoxelCore is a voxel-storage C# library for 2D and 3D voxel fields. It works in .NET and Unity without depending on `UnityEngine`. Worlds create chunks on demand, can mix dense and sparse storage, and serialize individual chunks to byte arrays.

As of now, the voxel data is limited to unmanaged only types.

The API uses two type parameters: `T` is an unmanaged C# cell value, such as `bool`, `int`, or a custom unmanaged struct. `K` is the numeric type for local sparse-chunk coordinates and must hold `0` through `chunkSize - 1`. For a size-16 Boolean world, use `sbyte` for `K` and `bool` for `T`.

## Create, edit, and serialize a 3D world

Place the statements in a method; put the `using` directives at the top of its C# file.

```csharp
using System.Collections.Generic;
using PiDev.VoxelCore;

var area = new Range3D(-5, 5, 0, 1, -5, 5);
var world = new World3D<sbyte, bool>(
    _ => new SparseChunk3D<sbyte, bool>(16, false),
    chunkSize: 16);

foreach (var point in area)
    world[point.x, point.y, point.z] = true;

world.OptimizeAllChunks();

var serializer = world.GetSerializer(null);
var serializedChunks = new Dictionary<(int x, int y, int z), byte[]>();
foreach (var entry in world.EnumerateChunks())
    serializedChunks[entry.Key] = serializer.SerializeOptimal(entry.Value, exact: true);

var deserializer = new ChunkDeserializer3D<sbyte, bool>(world.ChunkSize);
foreach (var entry in serializedChunks)
{
    IChunk3D<bool> chunk = deserializer.Deserialize(entry.Value, optimize: false);
    // entry.Key locates this chunk in the world; save it alongside the bytes.
}
```

`World3D` exposes its chunks through `EnumerateChunks()`, not a public `chunks` field. Save each chunk key and the world chunk size separately: neither is part of the chunk byte array. The current world API does not expose a method for inserting a deserialized chunk back into its private dictionary.

## 2D storage

```csharp
var world = new World2D<byte, int>(
    _ => new SparseChunk2D<byte, int>(16, 0),
    chunkSize: 16,
    defaultValue: 0);

world[-1, 3] = 7;
world.OptimizeAllChunks();

var serializer = world.GetSerializer(null);
foreach (var entry in world.EnumerateChunks())
{
    byte[] data = serializer.SerializeOptimal(entry.Value, exact: true);
    IChunk2D<int> restored = new ChunkDeserializer2D<byte, int>(16)
        .Deserialize(data, optimize: false);
}
```

`GetSerializer(replacements)` can remap values as it writes. `GetFastSerializer()` writes concrete dense or sparse chunks directly without replacements.

`SerializeOptimal(chunk, exact)` has two selection modes:

| `exact` | Selection | Cost |
| --- | --- | --- |
| `false` | Estimates dense versus sparse size; does not consider RLE. | Less serialization work, but the chosen output may not be the smallest. |
| `true` | Builds dense, sparse, and RLE byte arrays and returns the shortest. | More work and temporary memory. |

## Chunk defaults and optimization

Dense chunks allocate their cell buffer on the first write. Before then, all cells read as `DefaultValue`. Sparse chunks store only cells different from `DefaultValue`; setting a cell to that value removes its entry.

`DenseChunk2D/3D.Optimize` and `SparseChunk2D/3D.Optimize` return the same chunk when no conversion helps, a replacement chunk when one helps, or `null` when all cells equal the supplied default and the chunk can be removed. Without an argument, a chunk may choose its most common value as its new sparse default. Worlds always pass their own `defaultValue` and remove entries for which optimization returns `null`. `OptimizeAllChunks()` applies that behavior to all loaded chunks.

`Range2D` and `Range3D` include both minimum and maximum coordinates in containment checks and enumeration. Their `width`, `height`, and `depth` properties are `max - min`, rather than the number of integer positions visited.

## Chunk byte formats

Every byte array begins with a one-byte format number and a raw `T` default value. Cell data uses x as the fastest-varying coordinate.

| Format | Content |
| --- | --- |
| `0` | Dense cells as raw `T` values. |
| `1` | Sparse entries with raw `K` coordinates and raw `T` values. |
| `2` | Sparse entries with packed coordinates; only for size-16 chunks. |
| `3` | RLE runs with whole raw `T` values. |
| `4` | Default value only; a dense chunk whose buffer is still null. |

RLE stores each count as `run length - 1`. Chunks of at most 256 cells use one-byte counts. Chunks of 257 through 65,536 cells use groups of up to eight runs: one control byte marks which counts occupy two bytes, followed by the counts and then their `T` values. Larger chunks use a fixed count width based on their cell count. The final group ends at the end of the byte array. The encoder accepts any `IEnumerable<T>` of exactly the declared number of cells and consumes it once.

These bytes contain raw unmanaged representations, with no schema or version field. Keep the chunk size, coordinate key type, value type, and format version with persisted data. Previously written RLE blobs may use an incompatible layout.
