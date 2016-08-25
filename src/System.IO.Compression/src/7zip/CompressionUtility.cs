// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using SevenZip;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace System.IO.Compression
{
    public static class CompressionUtility
    {
        enum MeasureBy
        {
            Input,
            Output
        }

        [DllImport("clrcompression.dll")]
        private extern unsafe static int LZMAEncodeFile(string input, string output);

        [DllImport("clrcompression.dll")]
        private extern unsafe static int LZMADecodeFile(string input, string output);

        public static void Compress(Stream inStream, Stream outStream)
        {
            SevenZip.Compression.LZMA.Encoder encoder = new SevenZip.Compression.LZMA.Encoder();

            //CoderPropID[] propIDs =
            //{
            //    CoderPropID.DefaultProp
            //    //CoderPropID.DictionarySize,
            //    //CoderPropID.PosStateBits,
            //    //CoderPropID.LitContextBits,
            //    //CoderPropID.LitPosBits,
            //    //CoderPropID.Algorithm,
            //    //CoderPropID.NumFastBytes,
            //    //CoderPropID.MatchFinder,
            //    //CoderPropID.EndMarker
            //};
            //object[] properties =
            //{
            //        (Int32)(1 << 26),
            //        (Int32)(1),
            //        (Int32)(8),
            //        (Int32)(0),
            //        (Int32)(2),
            //        (Int32)(96),
            //        "bt4",
            //        false
            // };

            //encoder.SetCoderProperties(propIDs, properties);
            encoder.WriteCoderProperties(outStream);

            Int64 inSize = inStream.Length;
            for (int i = 0; i < 8; i++)
            {
                outStream.WriteByte((Byte)(inSize >> (8 * i)));
            }

            encoder.Code(inStream, outStream, -1, -1);
        }

        public static int CompressNative(string input, string output)
        {
            return LZMAEncodeFile(input, output);
        }

        public static int DecompressNative(string input, string output)
        {
            return LZMADecodeFile(input, output);
        }

        public static void Decompress(Stream inStream, Stream outStream)
        {
            byte[] properties = new byte[5];

            if (inStream.Read(properties, 0, 5) != 5)
                throw (new Exception("input .lzma is too short"));

            SevenZip.Compression.LZMA.Decoder decoder = new SevenZip.Compression.LZMA.Decoder();
            decoder.SetDecoderProperties(properties);

            long outSize = 0;
            for (int i = 0; i < 8; i++)
            {
                int v = inStream.ReadByte();
                if (v < 0)
                    throw (new Exception("Can't Read 1"));
                outSize |= ((long)(byte)v) << (8 * i);
            }

            long compressedSize = inStream.Length - inStream.Position;
            decoder.Code(inStream, outStream, compressedSize, outSize);
        }
    }
}