using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Windows.UI;

namespace Audiomatic.Visualizer;

internal static class EffectsHelper
{
    internal static Color ParseColor(string hex)
    {
        hex = (hex ?? "").TrimStart('#');
        if (hex.Length != 6)
            return Color.FromArgb(255, 96, 165, 250);
        return Color.FromArgb(255,
            Convert.ToByte(hex[..2], 16),
            Convert.ToByte(hex[2..4], 16),
            Convert.ToByte(hex[4..6], 16));
    }

    internal static Color GetRenderColor(VisualizerSettings settings)
    {
        if (!string.IsNullOrEmpty(settings.Color) && settings.Color.StartsWith('#') && settings.Color.Length == 7)
            return ParseColor(settings.Color);
        return Color.FromArgb(255, 96, 165, 250);
    }

    internal static void DrawDarkBackground(CanvasDrawingSession session, float width, float height)
    {
        session.FillRectangle(0, 0, width, height, Color.FromArgb(76, 0, 0, 0));
    }

    internal static void DrawGlow(CanvasDrawingSession session, CanvasCommandList commandList, float blurAmount = 12f)
    {
        var blur = new GaussianBlurEffect
        {
            Source = commandList,
            BlurAmount = blurAmount,
            BorderMode = EffectBorderMode.Soft
        };
        session.DrawImage(blur);
    }

    internal static void DrawReflection(CanvasDrawingSession session, CanvasCommandList commandList,
        float width, float height, float centerY, float opacity = 0.3f)
    {
        var transform = new Transform2DEffect
        {
            Source = commandList,
            TransformMatrix = Matrix3x2.CreateScale(1, -1, new Vector2(0, centerY))
        };
        var blur = new GaussianBlurEffect
        {
            Source = transform,
            BlurAmount = 4f
        };
        session.DrawImage(blur, 0, 0, new Windows.Foundation.Rect(0, centerY, width, height - centerY), opacity);
    }
}
