using Microsoft.Graphics.Canvas;

namespace Audiomatic.Visualizer;

public record VisualizerSettings(
    string Color,
    bool GlowEnabled,
    bool DarkBackground);

public interface IVisualizerMode
{
    int GetBandCount(float width, float height);

    void Render(CanvasDrawingSession session, float[] bands, float width, float height,
        VisualizerSettings settings);
}
