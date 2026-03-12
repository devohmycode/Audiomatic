using System.Numerics;
using Microsoft.Graphics.Canvas;
using Windows.UI;

namespace Audiomatic.Visualizer;

public sealed class WaveMode : IVisualizerMode
{
    private const int Cols = 64;
    private const int Rows = 16;
    private const float Perspective = 0.6f;

    private readonly float[][] _history = new float[Rows][];
    private int _historyIndex;
    private int _frameCount;

    public int GetBandCount(float width, float height) => Cols;

    public void Render(CanvasDrawingSession session, float[] bands, float width, float height,
        VisualizerSettings settings)
    {
        var baseColor = EffectsHelper.GetRenderColor(settings);
        int bandCount = Math.Min(bands.Length, Cols);

        if (_history[0] == null || _history[0].Length != bandCount)
        {
            for (int i = 0; i < Rows; i++)
                _history[i] = new float[bandCount];
        }
        Array.Copy(bands, 0, _history[_historyIndex], 0, bandCount);
        _historyIndex = (_historyIndex + 1) % Rows;
        _frameCount++;

        if (settings.DarkBackground)
            EffectsHelper.DrawDarkBackground(session, width, height);

        using var glowLayer = settings.GlowEnabled ? new CanvasCommandList(session) : null;
        using var drawSession = settings.GlowEnabled ? glowLayer!.CreateDrawingSession() : null;
        var ds = drawSession ?? session;

        float baseY = height * 0.7f;
        float maxH = height * 0.4f;
        float totalWidth = width * 0.8f;

        for (int row = Rows - 1; row >= 0; row--)
        {
            int histIdx = ((_historyIndex - 1 - row) % Rows + Rows) % Rows;
            var rowBands = _history[histIdx];
            if (_frameCount <= row) continue;

            float depthT = row / (float)(Rows - 1);
            float scale = 1f - depthT * Perspective;
            float rowY = baseY - row * (height * 0.025f);
            float alpha = 1f - depthT * 0.7f;
            var color = Color.FromArgb((byte)(255 * alpha), baseColor.R, baseColor.G, baseColor.B);

            float rowWidth = totalWidth * scale;
            float offsetX = (width - rowWidth) / 2f;
            float step = rowWidth / bandCount;

            Vector2? prev = null;
            for (int i = 0; i < bandCount && i < rowBands.Length; i++)
            {
                float x = offsetX + i * step;
                float barH = rowBands[i] * maxH * scale;
                float y = rowY - barH;

                var point = new Vector2(x, y);
                if (prev.HasValue)
                    ds.DrawLine(prev.Value, point, color, 1.5f * scale);
                prev = point;
            }
        }

        if (settings.GlowEnabled && glowLayer != null)
        {
            EffectsHelper.DrawGlow(session, glowLayer, 10f);
            session.DrawImage(glowLayer);
            EffectsHelper.DrawReflection(session, glowLayer, width, height, baseY);
        }
    }
}
