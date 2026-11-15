using System;

namespace PiDev.VoxelCore
{
    /// <summary>Caller-supplied conversion between a whole chunk and its byte representation.</summary>
    /// <typeparam name="TChunk">The chunk interface returned by deserialization.</typeparam>
    /// <example><![CDATA[
    /// var codec = new GenericSerializer<IChunk3D<string>>(EncodeChunk, DecodeChunk);
    /// byte[] bytes = codec.Serialize(chunk);
    /// IChunk3D<string> restored = codec.Deserialize(bytes);
    /// // EncodeChunk and DecodeChunk are supplied by the caller.
    /// ]]></example>
    public class GenericSerializer<TChunk>
    {
        private readonly Func<TChunk, byte[]> serialize;
        private readonly Func<byte[], TChunk> deserialize;

        /// <summary>Creates a whole-chunk serializer from encoding and decoding functions.</summary>
        public GenericSerializer(Func<TChunk, byte[]> serialize, Func<byte[], TChunk> deserialize)
        {
            this.serialize = serialize ?? throw new ArgumentNullException(nameof(serialize));
            this.deserialize = deserialize ?? throw new ArgumentNullException(nameof(deserialize));
        }

        /// <summary>Converts a chunk to caller-defined bytes.</summary>
        public byte[] Serialize(TChunk chunk) => serialize(chunk);

        /// <summary>Creates a chunk from bytes using the caller-supplied conversion.</summary>
        public TChunk Deserialize(byte[] data) => deserialize(data);
    }
}
