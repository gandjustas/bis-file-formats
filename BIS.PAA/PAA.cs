﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using BIS.Core.Streams;

namespace BIS.PAA
{
    public enum PAAType
    {
        DXT1 = 1,
        DXT2 = 2,
        DXT3 = 3,
        DXT4 = 4,
        DXT5 = 5,
        RGBA_5551,
        RGBA_4444,
        AI88,
        RGBA_8888,
        P8, //8bit index to palette

        UNDEFINED = -1
    }

    public class PAA
    {
        private List<Mipmap> mipmaps;
        private int[] mipmapOffsets = new int[16];

        public PAAType Type { get; private set; } = PAAType.UNDEFINED;
        public Palette Palette { get; private set; }

        public int Width => mipmaps[0].Width;
        public int Height => mipmaps[0].Height;


        public PAA(string file) : this(File.OpenRead(file), !file.EndsWith(".pac")) { }
        public PAA(Stream stream, bool isPac = false) : this(new BinaryReaderEx(stream), isPac) { }

        public PAA(BinaryReaderEx stream, bool isPac = false)
        {
            Read(stream, isPac);
        }

        public Mipmap this[int i] => mipmaps[i];

        private static PAAType MagicNumberToType(ushort magic)
        {
            switch (magic)
            {
                case 0x4444: return PAAType.RGBA_4444;
                case 0x8080: return PAAType.AI88;
                case 0x1555: return PAAType.RGBA_5551;
                case 0xff01: return PAAType.DXT1;
                case 0xff02: return PAAType.DXT2;
                case 0xff03: return PAAType.DXT3;
                case 0xff04: return PAAType.DXT4;
                case 0xff05: return PAAType.DXT5;
            }

            return PAAType.UNDEFINED;
        }

        private void Read(BinaryReaderEx input, bool isPac = false)
        {
            var magic = input.ReadUInt16();
            var type = MagicNumberToType(magic);
            if (type == PAAType.UNDEFINED)
            {
                type = (!isPac) ? PAAType.RGBA_4444 : PAAType.P8;
                input.Position -= 2;
            }
            Type = type;

            Palette = new Palette(type);
            Palette.Read(input, mipmapOffsets);
            
            mipmaps = new List<Mipmap>(16);
            int i = 0;
            while (input.ReadUInt32() != 0)
            {
                input.Position -= 4;

                Debug.Assert(input.Position == mipmapOffsets[i]);
                var mipmap = new Mipmap(input, mipmapOffsets[i++]);
                mipmaps.Add(mipmap);
            }
            if (input.ReadUInt16() != 0)
                throw new FormatException("Expected two more zero's at end of file.");
        }

        public static byte[] GetARGB32PixelData(Stream paaStream, bool isPac = false, int mipmapIndex = 0)
        {
            var paa = new PAA(paaStream, isPac);
            return GetARGB32PixelData(paa, paaStream, mipmapIndex);
        }

        public static byte[] GetARGB32PixelData(PAA paa, Stream paaStream, int mipmapIndex = 0)
        {
            var input = new BinaryReaderEx(paaStream);

            Mipmap mipmap = paa[mipmapIndex];
            var rawData = mipmap.GetRawPixelData(input, paa.Type);

            switch (paa.Type)
            {
                case PAAType.RGBA_8888:
                case PAAType.P8:
                    return PixelFormatConversion.P8ToARGB32(rawData, paa.Palette);
                case PAAType.DXT1:
                case PAAType.DXT2:
                case PAAType.DXT3:
                case PAAType.DXT4:
                case PAAType.DXT5:
                    return PixelFormatConversion.DXTToARGB32(rawData, mipmap.Width, mipmap.Height, (int)paa.Type);
                case PAAType.RGBA_4444:
                    return PixelFormatConversion.ARGB16ToARGB32(rawData);
                case PAAType.RGBA_5551:
                    return PixelFormatConversion.ARGB1555ToARGB32(rawData);
                case PAAType.AI88:
                    return PixelFormatConversion.AI88ToARGB32(rawData);
                default:
                    throw new Exception($"Cannot retrieve pixel data from this PaaType: {paa.Type}");
            }
        }
    }
}