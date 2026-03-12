using Microsoft.Graphics.Canvas;
using Windows.UI;

namespace Audiomatic.Visualizer;

public sealed class CircleMode : IVisualizerMode
{
    private const int BandCount = 64;
    private const float MinRadius = 0.15f;
    private const float MaxBarLength = 0.30f;
    private const float BarWidth = 3f;

    private float _rotation;

    public int GetBandCount(float width, float height) => BandCount;

    public void Render(CanvasDrawingSession session, float[] bands, float width, float height,
        VisualizerSettings settings)
    {
        var baseColor = EffectsHelper.GetRenderColor(settings);
        float cx = width / 2f;
        float cy = height / 2f;
        float radius = MathF.Min(width, height) / 2f;
        float innerR = radius * MinRadius;
        float maxLen = radius * MaxBarLength;

        _rotation += 0.003f;

        if (settings.DarkBackground)
            EffectsHelper.DrawDarkBackground(session, width, height);

        using var glowLayer = settings.GlowEnabled ? new CanvasCommandList(session) : null;
        using var drawSession = settings.GlowEnabled ? glowLayer!.CreateDrawingSession() : null;
        var ds = drawSession ?? session;

        int count = bands.Length;
        float angleStep = MathF.Tau / count;

        for (int i = 0; i < count; i++)
        {
            float angle = i * angleStep + _rotation;
            float barLen = Math.Max(2f, bands[i] * maxLen);
            float alpha = 0.5f + bands[i] * 0.5f;

            float cos = MathF.Cos(angle);
            float sin = MathF.Sin(angle);

            float x1 = cx + cos * innerR;
            float y1 = cy + sin * innerR;
            float x2 = cx + cos * (innerR + barLen);
            float y2 = cy + sin * (innerR + barLen);

            var color = Color.FromArgb((byte)(255 * alpha), baseColor.R, baseColor.G, baseColor.B);
            ds.DrawLine(x1, y1, x2, y2, color, BarWidth);
        }

        if (settings.GlowEnabled && glowLayer != null)
        {
            EffectsHelper.DrawGlow(session, glowLayer, 16f);
            session.DrawImage(glowLayer);
            EffectsHelper.DrawReflection(session, glowLayer, width, height, cy);
        }
    }
}
