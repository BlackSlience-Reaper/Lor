using System;
using System.Collections.Generic;
using Godot;

namespace LibraryOfRuina.patches;

internal static class ReverberationEnsembleIconFill
{
    // 接待图标以低透明度线条作为边界，保留缩小后较细的原始轮廓。
    private const float BoundaryAlphaThreshold = 0.08f;

    // 闭合源图抗锯齿产生的一像素断口，避免内部填充泄漏到背景。
    private const int BoundaryClosingRadius = 1;

    private static readonly Dictionary<string, Texture2D> Masks = new();

    internal static Texture2D GetMask(string path, Texture2D texture, bool extractLines)
    {
        if (Masks.TryGetValue(path, out Texture2D? cached))
        {
            return cached;
        }

        using Image source = texture.GetImage();
        if (source.IsCompressed() && source.Decompress() != Error.Ok)
        {
            throw new InvalidOperationException($"Cannot read reception icon: {path}");
        }

        int width = source.GetWidth();
        int height = source.GetHeight();
        bool[] boundary = new bool[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color pixel = source.GetPixel(x, y);
                float alpha = pixel.A;
                if (extractLines)
                {
                    // 与显示 Shader 共用宗教层原图的去光晕阈值。
                    float signal = Mathf.Clamp((pixel.B - 0.45f) / (0.75f - 0.45f), 0f, 1f);
                    alpha *= signal * signal * (3f - 2f * signal);
                }

                if (alpha < BoundaryAlphaThreshold)
                {
                    continue;
                }

                for (int dy = -BoundaryClosingRadius; dy <= BoundaryClosingRadius; dy++)
                {
                    for (int dx = -BoundaryClosingRadius; dx <= BoundaryClosingRadius; dx++)
                    {
                        int sampleX = x + dx;
                        int sampleY = y + dy;
                        if (sampleX >= 0 && sampleX < width && sampleY >= 0 && sampleY < height)
                        {
                            boundary[sampleY * width + sampleX] = true;
                        }
                    }
                }
            }
        }

        // 从画布外缘查找背景，只填充线条及其封闭区域，保留外侧透明度。
        bool[] exterior = new bool[boundary.Length];
        Queue<int> pending = new();
        void Visit(int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
            {
                return;
            }

            int index = y * width + x;
            if (!boundary[index] && !exterior[index])
            {
                exterior[index] = true;
                pending.Enqueue(index);
            }
        }

        for (int x = 0; x < width; x++)
        {
            Visit(x, 0);
            Visit(x, height - 1);
        }

        for (int y = 0; y < height; y++)
        {
            Visit(0, y);
            Visit(width - 1, y);
        }

        while (pending.TryDequeue(out int index))
        {
            int x = index % width;
            int y = index / width;
            Visit(x - 1, y);
            Visit(x + 1, y);
            Visit(x, y - 1);
            Visit(x, y + 1);
        }

        FillOpenInteriors(boundary, exterior, width, height);

        byte[] pixels = new byte[boundary.Length * 4];
        for (int index = 0; index < boundary.Length; index++)
        {
            int offset = index * 4;
            pixels[offset] = 255;
            pixels[offset + 1] = 255;
            pixels[offset + 2] = 255;
            pixels[offset + 3] = exterior[index] ? (byte)0 : (byte)255;
        }

        using Image mask = Image.CreateFromData(width, height, false, Image.Format.Rgba8, pixels);
        Texture2D result = ImageTexture.CreateFromImage(mask);
        Masks.Add(path, result);
        return result;
    }

    private static void FillOpenInteriors(bool[] boundary, bool[] exterior, int width, int height)
    {
        // 齿轮和钟面带有设计上的开口。以四个方向的最外侧线条共同界定内部，
        // 补上会与背景连通的镂空，同时保留外侧凹口和独立装饰的透明间隔。
        int[] rowLeft = new int[height];
        int[] rowRight = new int[height];
        int[] columnTop = new int[width];
        int[] columnBottom = new int[width];
        Array.Fill(rowLeft, width);
        Array.Fill(rowRight, -1);
        Array.Fill(columnTop, height);
        Array.Fill(columnBottom, -1);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!boundary[y * width + x])
                {
                    continue;
                }

                rowLeft[y] = Math.Min(rowLeft[y], x);
                rowRight[y] = Math.Max(rowRight[y], x);
                columnTop[x] = Math.Min(columnTop[x], y);
                columnBottom[x] = Math.Max(columnBottom[x], y);
            }
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = rowLeft[y]; x <= rowRight[y]; x++)
            {
                if (y >= columnTop[x] && y <= columnBottom[x])
                {
                    exterior[y * width + x] = false;
                }
            }
        }
    }
}
