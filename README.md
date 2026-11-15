# VoxelCore

VoxelCore is a voxel data storage library for C#. It supports chunked 2D and 3D data fields of any type. Works in .NET and Unity without depending on `UnityEngine`. Worlds create chunks on demand and can mix dense, sparse and custom storage.

There are two storage families. `World2DFast` and `World3DFast` use unmanaged cell values and provide the raw chunk serializers described below. `World2D` and `World3D` accept arbitrary C# values, including objects and null, in managed chunks. Managed chunks use a supplied comparer and an optional whole-chunk serializer.

The API uses two type parameters. `K` is the numeric type for local sparse-chunk coordinates and must hold `0` through `chunkSize - 1`. `T` is the cell value: it must be unmanaged in the Fast family and is unconstrained in the managed family. For a size-16 Boolean Fast world, `sbyte` and `bool` work as `K` and `T`.

## Create, edit, and serialize a Fast 3D world

```csharp
using System.Collections.Generic;
using PiDev.VoxelCore;

var area = new Range3D(-5, 5, 0, 1, -5, 5);
var world = new World3DFast<sbyte, bool>(
    _ => new SparseChunk3DFast<sbyte, bool>(16, false),
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
    IChunk3DFast<bool> chunk = deserializer.Deserialize(entry.Value, optimize: false);
    // entry.Key locates this chunk in the world; save it alongside the bytes.
}
```

`World3DFast` exposes its chunks through `EnumerateChunks()`. Save each chunk key and the world chunk size separately: neither is part of the chunk byte array. The current world API does not expose a method for inserting a deserialized chunk back into its private dictionary.

## Fast 2D storage

```csharp
var world = new World2DFast<byte, int>(
    _ => new SparseChunk2DFast<byte, int>(16, 0),
    chunkSize: 16,
    defaultValue: 0);

world[-1, 3] = 7;
world.OptimizeAllChunks();

var serializer = world.GetSerializer(null);
foreach (var entry in world.EnumerateChunks())
{
    byte[] data = serializer.SerializeOptimal(entry.Value, exact: true);
    IChunk2DFast<int> restored = new ChunkDeserializer2D<byte, int>(16)
        .Deserialize(data, optimize: false);
}
```

`GetSerializer(replacements)` can remap values as it writes. `GetFastSerializer()` writes concrete dense or sparse chunks directly without replacements.

## Managed values

```csharp
using System;
using PiDev.VoxelCore;

var world = new World3D<byte, string>(
    _ => new SparseChunk3D<byte, string>(16, "empty", StringComparer.Ordinal),
    chunkSize: 16,
    defaultValue: "empty");
world[0, 0, 0] = "stone";
world.OptimizeAllChunks();
```

Managed chunks default to `EqualityComparer<T>.Default` when no comparer is supplied. For a custom representation, supply a `GenericSerializer<IChunk3D<T>>` or `GenericSerializer<IChunk2D<T>>` to the chunk constructor. Its two caller-defined functions convert an entire chunk to bytes and bytes back to a chunk:

```csharp
var codec = new GenericSerializer<IChunk3D<string>>(SerializeChunk, DeserializeChunk);
var chunk = new SparseChunk3D<byte, string>(16, "empty", StringComparer.Ordinal, codec);
byte[] bytes = chunk.Serialize();
IChunk3D<string> restored = codec.Deserialize(bytes);
```

`SerializeChunk` and `DeserializeChunk` are application-provided functions. Without a codec, `chunk.Serialize()` throws `NotSupportedException`. Deserialization lives on the codec because C# 10 cannot require a static `Deserialize` method on the chunk interface.

`SerializeOptimal(chunk, exact)` has two selection modes:

| `exact` | Selection | Cost |
| --- | --- | --- |
| `false` | Estimates dense versus sparse size; does not consider RLE. | Less serialization work, but the chosen output may not be the smallest. |
| `true` | Builds dense, sparse, and RLE byte arrays and returns the shortest. | More work and temporary memory. |

## Chunk defaults and optimization

Dense chunks allocate their cell buffer on the first write. Before then, all cells read as `DefaultValue`. Sparse chunks store only cells different from `DefaultValue`; setting a cell to that value removes its entry.

`Optimize` returns the same chunk when no conversion helps, a replacement chunk when one helps, or `null` when all cells equal an explicitly supplied default and the chunk can be removed. Without an argument, a chunk may choose its most common value as its new sparse default. Worlds always pass their own `defaultValue` and remove entries for which optimization returns `null`. `OptimizeAllChunks()` applies that behavior to all loaded chunks. Managed dense chunks convert to sparse only when fewer than one quarter of cells differ from the chosen default.

`Range2D` and `Range3D` include both minimum and maximum coordinates in containment checks and enumeration. Their `width`, `height`, and `depth` properties are `max - min`, rather than the number of integer positions visited.

## Fast chunk byte formats

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
