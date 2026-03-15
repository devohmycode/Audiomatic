using NAudio.Dsp;
using NAudio.Wave;

namespace Audiomatic.Services;

public class Equalizer : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly int _channels;
    private readonly int _sampleRate;
    private readonly int _bandCount;
    private BiQuadFilter[,] _filters;
    private readonly float[] _gains;
    private float _preamp = 1.0f;
    private bool _enabled = true;

    public static readonly float[] DefaultFrequencies =
        [32f, 64f, 125f, 250f, 500f, 1000f, 2000f, 4000f, 8000f, 16000f];

    public static readonly string[] FrequencyLabels =
        ["32", "64", "125", "250", "500", "1K", "2K", "4K", "8K", "16K"];

    public static readonly Dictionary<string, float[]> Presets = new()
    {
        ["Flat"] = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
        ["Bass Boost"] = [6, 5, 4, 2, 0, 0, 0, 0, 0, 0],
        ["Treble Boost"] = [0, 0, 0, 0, 0, 0, 2, 4, 5, 6],
        ["Rock"] = [5, 4, 3, 1, -1, 0, 2, 3, 4, 5],
        ["Pop"] = [-1, 1, 3, 4, 3, 0, -1, 1, 2, 3],
        ["Jazz"] = [3, 2, 1, 2, -1, -1, 0, 1, 2, 3],
        ["Classical"] = [4, 3, 2, 1, -1, -1, 0, 2, 3, 4],
        ["Electronic"] = [5, 4, 1, 0, -2, 0, 1, 3, 4, 5],
        ["Hip-Hop"] = [5, 4, 3, 1, 0, 0, 1, 0, 2, 3],
        ["Vocal"] = [-2, -1, 0, 2, 4, 4, 3, 1, 0, -1],
    };

    public Equalizer(ISampleProvider source)
    {
        _source = source;
        _channels = source.WaveFormat.Channels;
        _sampleRate = source.WaveFormat.SampleRate;
        _bandCount = DefaultFrequencies.Length;
        _gains = new float[_bandCount];
        _filters = new BiQuadFilter[_channels, _bandCount];
        RebuildFilters();
    }

    public WaveFormat WaveFormat => _source.WaveFormat;
    public bool Enabled { get => _enabled; set => _enabled = value; }

    public float Preamp
    {
        get => _preamp;
        set => _preamp = Math.Clamp(value, 0.1f, 4.0f);
    }

    private void RebuildFilters()
    {
        _filters = new BiQuadFilter[_channels, _bandCount];
        for (int c = 0; c < _channels; c++)
        {
            for (int b = 0; b < _bandCount; b++)
            {
                _filters[c, b] = BiQuadFilter.PeakingEQ(
                    _sampleRate, DefaultFrequencies[b], 1.0f, _gains[b]);
            }
        }
    }

    public void SetBand(int index, float gainDb)
    {
        if (index < 0 || index >= _bandCount) return;
        _gains[index] = Math.Clamp(gainDb, -12f, 12f);
        for (int c = 0; c < _channels; c++)
        {
            _filters[c, index] = BiQuadFilter.PeakingEQ(
                _sampleRate, DefaultFrequencies[index], 1.0f, _gains[index]);
        }
    }

    public void SetAllBands(float[] gains)
    {
        for (int i = 0; i < Math.Min(gains.Length, _bandCount); i++)
            _gains[i] = Math.Clamp(gains[i], -12f, 12f);
        RebuildFilters();
    }

    public float[] GetGains()
    {
        return (float[])_gains.Clone();
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int read = _source.Read(buffer, offset, count);
        if (!_enabled) return read;

        for (int i = 0; i < read; i++)
        {
            int ch = i % _channels;
            float sample = buffer[offset + i];
            for (int b = 0; b < _bandCount; b++)
                sample = _filters[ch, b].Transform(sample);
            buffer[offset + i] = sample * _preamp;
        }

        return read;
    }
}
