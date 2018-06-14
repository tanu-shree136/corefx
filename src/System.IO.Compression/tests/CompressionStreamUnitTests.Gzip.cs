// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace System.IO.Compression
{
    public class GzipStreamUnitTests : CompressionStreamUnitTestBase
    {
        public override Stream CreateStream(Stream stream, CompressionMode mode) => new GZipStream(stream, mode);
        public override Stream CreateStream(Stream stream, CompressionMode mode, bool leaveOpen) => new GZipStream(stream, mode, leaveOpen);
        public override Stream CreateStream(Stream stream, CompressionLevel level) => new GZipStream(stream, level);
        public override Stream CreateStream(Stream stream, CompressionLevel level, bool leaveOpen) => new GZipStream(stream, level, leaveOpen);
        public override Stream BaseStream(Stream stream) => ((GZipStream)stream).BaseStream;
        protected override string CompressedTestFile(string uncompressedPath) => Path.Combine("GZipTestData", Path.GetFileName(uncompressedPath) + ".gz");

        [Fact]
        public void Precancellation()
        {
            var ms = new MemoryStream();
            using (Stream compressor = new GZipStream(ms, CompressionMode.Compress, leaveOpen: true))
            {
                Assert.True(compressor.WriteAsync(new byte[1], 0, 1, new CancellationToken(true)).IsCanceled);
                Assert.True(compressor.FlushAsync(new CancellationToken(true)).IsCanceled);
            }
            using (Stream decompressor = CreateStream(ms, CompressionMode.Decompress, leaveOpen: true))
            {
                Assert.True(decompressor.ReadAsync(new byte[1], 0, 1, new CancellationToken(true)).IsCanceled);
            }
        }

        [Fact]
        public void ConcatenatedGzipStreams()
        {
            using (MemoryStream concatStream = new MemoryStream())
            {
                using (var gz = new GZipStream(concatStream, CompressionLevel.NoCompression, true))
                using (var sw = new StreamWriter(gz))
                    sw.WriteLine("Stream 1");

                using (var gz = new GZipStream(concatStream, CompressionLevel.NoCompression, true))
                using (var sw = new StreamWriter(gz))
                    sw.WriteLine("Stream 2");

                new GZipStream(concatStream, CompressionLevel.NoCompression, true).Dispose();

                concatStream.Seek(0, SeekOrigin.Begin);
                using (var gz = new GZipStream(concatStream, CompressionMode.Decompress))
                using (var sr = new StreamReader(gz))
                {
                    Assert.Equal("Stream 1", sr.ReadLine());
                    Assert.Equal("Stream 2", sr.ReadLine());
                    Assert.Empty(sr.ReadToEnd());
                }
            }
        }

        [Fact]
        public async void ManyConcatenatedGzipStreams()
        {
            await TestSingleItemConcatenatedGzipStreamsAsync(1000, false, true);
            await TestSingleItemConcatenatedGzipStreamsAsync(1000, true, false);
            await TestSingleItemConcatenatedGzipStreamsAsync(1000, false, false);
        }

        [Fact]
        [OuterLoop]
        public async void ManyManyConcatenatedGzipStreams()
        {
            await TestSingleItemConcatenatedGzipStreamsAsync(400000, false, true);
        }

        private async Task TestSingleItemConcatenatedGzipStreamsAsync(int count, bool useAsync, bool useReadByte)
        {
            using (MemoryStream concatStream = new MemoryStream())
            {
                for (int i = 0; i < count; i++)
                {
                    using (var gz = new GZipStream(concatStream, CompressionLevel.NoCompression, true))
                    {
                        gz.Write(new byte[] { (byte)(i % 256) });
                    }
                }

                concatStream.Seek(0, SeekOrigin.Begin);
                byte[] decompressed = new byte[count + 1000];
                using (var gz = new GZipStream(concatStream, CompressionMode.Decompress))
                {
                    if (useReadByte)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            decompressed[i] = (byte)gz.ReadByte();
                        }
                    }
                    else if (useAsync)
                    {
                        gz.ReadAsync(decompressed).AsTask().Wait();
                        int index = 0, read = 0;
                        while ((read = await gz.ReadAsync(decompressed, index, decompressed.Length - index)) > 0)
                        {
                            index += read;
                        }
                    }
                    else
                    {
                        gz.Read(decompressed);
                    }
                }

                for (int i = 0; i < count; i++)
                {
                    if ((byte)(i % 256) != decompressed[i])
                        throw new Exception(i + "");
                    Assert.Equal((byte)(i % 256), decompressed[i]);
                }
            }
        }

        [Fact]
        public void DerivedStream_ReadWriteSpan_UsesReadWriteArray()
        {
            var ms = new MemoryStream();
            using (var compressor = new DerivedGZipStream(ms, CompressionMode.Compress, leaveOpen: true))
            {
                compressor.Write(new Span<byte>(new byte[1]));
                Assert.True(compressor.WriteArrayInvoked);
            }
            ms.Position = 0;
            using (var compressor = new DerivedGZipStream(ms, CompressionMode.Decompress, leaveOpen: true))
            {
                compressor.Read(new Span<byte>(new byte[1]));
                Assert.True(compressor.ReadArrayInvoked);
            }
            ms.Position = 0;
            using (var compressor = new DerivedGZipStream(ms, CompressionMode.Decompress, leaveOpen: true))
            {
                compressor.ReadAsync(new Memory<byte>(new byte[1])).AsTask().Wait();
                Assert.True(compressor.ReadArrayInvoked);
            }
            ms.Position = 0;
            using (var compressor = new DerivedGZipStream(ms, CompressionMode.Compress, leaveOpen: true))
            {
                compressor.WriteAsync(new ReadOnlyMemory<byte>(new byte[1])).AsTask().Wait();
                Assert.True(compressor.WriteArrayInvoked);
            }
        }

        private sealed class DerivedGZipStream : GZipStream
        {
            public bool ReadArrayInvoked = false, WriteArrayInvoked = false;
            internal DerivedGZipStream(Stream stream, CompressionMode mode) : base(stream, mode) { }
            internal DerivedGZipStream(Stream stream, CompressionMode mode, bool leaveOpen) : base(stream, mode, leaveOpen) { }

            public override int Read(byte[] array, int offset, int count)
            {
                ReadArrayInvoked = true;
                return base.Read(array, offset, count);
            }

            public override Task<int> ReadAsync(byte[] array, int offset, int count, CancellationToken cancellationToken)
            {
                ReadArrayInvoked = true;
                return base.ReadAsync(array, offset, count, cancellationToken);
            }

            public override void Write(byte[] array, int offset, int count)
            {
                WriteArrayInvoked = true;
                base.Write(array, offset, count);
            }

            public override Task WriteAsync(byte[] array, int offset, int count, CancellationToken cancellationToken)
            {
                WriteArrayInvoked = true;
                return base.WriteAsync(array, offset, count, cancellationToken);
            }
        }
    }
}
