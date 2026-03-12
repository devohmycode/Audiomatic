using Microsoft.Graphics.Canvas;
using Windows.UI;

namespace Audiomatic.Visualizer;

public sealed class BarsMode : IVisualizerMode
{
    private const int DepthRows = 4;
    private const float BarWidth = 5f;
    private const float BarGap = 2f;
    private const float TiltAngle = 0.25f;
    private const float DepthSpacing = 14f;

    private readonly float[][] _history = new float[DepthRows][];
    private int _historyIndex;
    private int _frameCount;

    public int GetBandCount(float width, float height)
        => Math.Max(1, (int)(width / (BarWidth + BarGap)));

    public void Render(CanvasDrawingSession session, float[] bands, float width, float height,
        VisualizerSettings settings)
    {
        var baseColor = EffectsHelper.GetRenderColor(settings);
        float centerY = height * 0.55f;
        float halfMax = height * 0.40f;
        int bandCount = bands.Length;

        if (_history[0] == null || _history[0].Length != bandCount)
        {
            for (int i = 0; i < DepthRows; i++)
                _history[i] = new float[bandCount];
        }
        Array.Copy(bands, _history[_historyIndex], bandCount);
        _historyIndex = (_historyIndex + 1) % DepthRows;
        _frameCount++;

        if (settings.DarkBackground)
            EffectsHelper.DrawDarkBackground(session, width, height);

        using var glowLayer = settings.GlowEnabled ? new CanvasCommandList(session) : null;
        using var drawSession = settings.GlowEnabled ? glowLayer!.CreateDrawingSession() : null;
        var ds = drawSession ?? session;

        float totalWidth = bandCount * (BarWidth + BarGap);
        float offsetX = (width - totalWidth) / 2f;

        for (int row = DepthRows - 1; row >= 0; row--)
        {
            int histIdx = ((_historyIndex - 1 - row) % DepthRows + DepthRows) % DepthRows;
            var rowBands = _history[histIdx];
            if (_frameCount <= row) continue;

            float depthFactor = 1f - row * 0.18f;
            byte rowAlpha = (byte)(255 * (1f - row * 0.22f));
            float yOffset = row * DepthSpacing * TiltAngle;

            var color = Color.FromArgb(rowAlpha, baseColor.R, baseColor.G, baseColor.B);

            for (int i = 0; i < bandCount && i < rowBands.Length; i++)
            {
                float x = offsetX + i * (BarWidth + BarGap);
                float barH = Math.Max(2f, rowBands[i] * halfMax * depthFactor);
                float y = centerY - barH - yOffset;

                ds.FillRoundedRectangle(x, y, BarWidth * depthFactor, barH, 2, 2, color);
            }
        }

        if (settings.GlowEnabled && glowLayer != null)
        {
            EffectsHelper.DrawGlow(session, glowLayer);
            session.DrawImage(glowLayer);
            EffectsHelper.DrawReflection(session, glowLayer, width, height, centerY);
        }
    }
}
