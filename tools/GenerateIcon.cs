using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

internal static class GenerateIcon
{
    private static GraphicsPath Rounded(float x, float y, float w, float h, float radius)
    {
        GraphicsPath p = new GraphicsPath(); float d = radius * 2;
        p.AddArc(x, y, d, d, 180, 90); p.AddArc(x + w - d, y, d, d, 270, 90);
        p.AddArc(x + w - d, y + h - d, d, d, 0, 90); p.AddArc(x, y + h - d, d, d, 90, 90); p.CloseFigure(); return p;
    }
    private static byte[] Render(int size)
    {
        using (Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Color.Transparent); g.ScaleTransform(size / 64F, size / 64F);
            using (GraphicsPath bg = Rounded(2, 2, 60, 60, 13))
            using (Brush fill = new LinearGradientBrush(new Rectangle(0, 0, 64, 64), Color.FromArgb(0, 85, 255), Color.FromArgb(4, 20, 58), 80F)) g.FillPath(fill, bg);
            using (GraphicsPath display = Rounded(13, 17, 38, 26, 4))
            using (Pen white = new Pen(Color.White, 3))
            {
                g.DrawPath(white, display); g.DrawLine(white, 32, 44, 32, 51); g.DrawLine(white, 23, 51, 41, 51);
                g.DrawLines(white, new[] { new PointF(21, 27), new PointF(42, 27), new PointF(38, 23) });
                g.DrawLines(white, new[] { new PointF(43, 34), new PointF(22, 34), new PointF(26, 38) });
            }
            using (MemoryStream stream = new MemoryStream()) { bitmap.Save(stream, ImageFormat.Png); return stream.ToArray(); }
        }
    }
    private static void Main(string[] args)
    {
        int[] sizes = { 16, 24, 32, 48, 64, 128, 256 }; List<byte[]> images = new List<byte[]>();
        foreach (int size in sizes) images.Add(Render(size));
        using (BinaryWriter writer = new BinaryWriter(File.Create(args[0])))
        {
            writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                writer.Write((byte)0); writer.Write((byte)0); writer.Write((ushort)1); writer.Write((ushort)32);
                writer.Write(images[i].Length); writer.Write(offset); offset += images[i].Length;
            }
            foreach (byte[] image in images) writer.Write(image);
        }
    }
}
