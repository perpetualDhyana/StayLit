using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using StayLit;

internal static class MakeIcon
{
    public static void Main(string[] args)
    {
        int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
        byte[][] images = new byte[sizes.Length][];
        for (int i = 0; i < sizes.Length; i++)
            using (Bitmap bitmap = Branding.DrawIcon(sizes[i]))
            using (MemoryStream data = new MemoryStream())
            {
                bitmap.Save(data, ImageFormat.Png);
                images[i] = data.ToArray();
            }
        using (BinaryWriter output = new BinaryWriter(File.Create(args[0])))
        {
            output.Write((ushort)0); output.Write((ushort)1); output.Write((ushort)sizes.Length);
            int offset = 6 + sizes.Length * 16;
            for (int i = 0; i < sizes.Length; i++)
            {
                output.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                output.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                output.Write((byte)0); output.Write((byte)0);
                output.Write((ushort)1); output.Write((ushort)32);
                output.Write(images[i].Length); output.Write(offset);
                offset += images[i].Length;
            }
            foreach (byte[] image in images) output.Write(image);
        }
    }
}
