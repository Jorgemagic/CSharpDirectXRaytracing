using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using System.Runtime.InteropServices;
using Vortice.DXGI;

namespace RayTracingTutorial25.RTX
{
    public class TextureHelper
    {
        public struct Image
        {
            public uint Width;
            public uint Height;
            public Format Format;
            public uint TexturePixelSize;
            public byte[] Data;
        }

        public static Image Load(string filename)
        {
            Image result = default;

            using (var image = SixLabors.ImageSharp.Image.Load<Rgba32>(filename))
            {
                result.Width = (uint)image.Width;
                result.Height = (uint)image.Height;
                result.Format = Format.R8G8B8A8_UNorm;
                result.TexturePixelSize = 4;

                var pixels = image.GetPixelSpan();

                for (int i = 0; i < pixels.Length; i++)
                {
                    ref Rgba32 pixel = ref pixels[i];
                    var a = pixel.A;
                    if (a == 0)
                    {
                        pixel.PackedValue = 0;
                    }
                    else
                    {
                        pixel.R = (byte)((pixel.R * a) >> 8);
                        pixel.G = (byte)((pixel.G * a) >> 8);
                        pixel.B = (byte)((pixel.B * a) >> 8);
                    }
                }

                result.Data = MemoryMarshal.AsBytes(pixels).ToArray();
            }

            return result;
        }
    }
}
