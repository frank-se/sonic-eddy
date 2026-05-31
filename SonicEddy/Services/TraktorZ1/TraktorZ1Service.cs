using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Threading;
using Fr.Sonic.Model.Objects;
using Fr.Sonic.Model.Params;
using Microsoft.Extensions.Logging;
using SonicEddy.Audio;

namespace SonicEddy.Services.TraktorZ1;

public class TraktorZ1Service : ITraktorZ1Service
{
    private const byte  ReportIdInput   = 0x01;
    private const byte  ReportIdOutput  = 0x80;
    private const byte  BtnOnL          = 0x04;
    private const byte  BtnOnR          = 0x08;
    private const float PickUpThreshold = 0.02f;

    private const int IdxGainL  = 0;
    private const int IdxHiL    = 1;
    private const int IdxMidL   = 2;
    private const int IdxLowL   = 3;
    private const int IdxGainR  = 5;
    private const int IdxHiR    = 6;
    private const int IdxMidR   = 7;
    private const int IdxLowR   = 8;
    private const int IdxFaderL = 11;
    private const int IdxFaderR = 12;

    private static readonly int[] KnobByteOffsets =
        [1, 3, 5, 7, 9, 11, 13, 15, 17, 19, 21, 23, 25, 27];

    private readonly TraktorZ1SetupService _setup;
    private readonly ILogger<TraktorZ1Service> _logger;

    private readonly int[]    _selectedSection   = new int[2];
    private readonly bool[]   _faderCatchingUp   = [true, true];
    private readonly bool[][] _knobCatchingUp    = [[true, true, true, true],
                                                    [true, true, true, true]];
    private readonly ushort[] _prev              = new ushort[14];
    private byte _prevButtons;

    private volatile FileStream? _stream;
    private CancellationTokenSource? _cts;
    private Thread? _readThread;

    public event Action<TraktorZ1Side, int>? FilterSectionChanged;

    public TraktorZ1Service(TraktorZ1SetupService setup,
        ILogger<TraktorZ1Service> logger)
    {
        _setup  = setup;
        _logger = logger;
    }

    public void Start(string hidrawPath)
    {
        FileStream stream;
        try
        {
            stream = new FileStream(hidrawPath, FileMode.Open,
                FileAccess.ReadWrite, FileShare.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not open Traktor Z1 at {Path}: {Message}",
                hidrawPath, ex.Message);
            return;
        }

        ResetCatchingUp();

        _stream = stream;
        _cts    = new CancellationTokenSource();

        WriteLeds(stream);

        _readThread = new Thread(() => ReadLoop(stream, _cts.Token))
        {
            IsBackground = true,
            Name         = "TraktorZ1Reader"
        };
        _readThread.Start();

        _logger.LogInformation("Traktor Z1 started on {Path}", hidrawPath);
    }

    public void Stop()
    {
        _cts?.Cancel();
        var s = _stream;
        _stream = null;
        s?.Dispose();
        _readThread?.Join(TimeSpan.FromSeconds(2));
        _readThread = null;
    }

    public void Dispose() => Stop();

    private void ResetCatchingUp()
    {
        _faderCatchingUp[0] = _faderCatchingUp[1] = true;
        for (int s = 0; s < 2; s++)
        for (int p = 0; p < 4; p++)
            _knobCatchingUp[s][p] = true;
    }

    private void ReadLoop(FileStream stream, CancellationToken token)
    {
        var buf = new byte[30];
        try
        {
            while (!token.IsCancellationRequested)
            {
                int n = stream.Read(buf, 0, buf.Length);
                if (n == buf.Length && buf[0] == ReportIdInput)
                    ProcessReport(buf, stream);
            }
        }
        catch (ObjectDisposedException) { }
        catch (IOException) { }
    }

    private void ProcessReport(byte[] buf, FileStream stream)
    {
        var cur = new ushort[14];
        for (int i = 0; i < 14; i++)
            cur[i] = BinaryPrimitives.ReadUInt16LittleEndian(
                buf.AsSpan(KnobByteOffsets[i]));
        byte buttons = buf[29];

        if (cur[IdxFaderL] != _prev[IdxFaderL])
            SetVolume(TraktorZ1Side.Left, cur[IdxFaderL]);
        if (cur[IdxFaderR] != _prev[IdxFaderR])
            SetVolume(TraktorZ1Side.Right, cur[IdxFaderR]);

        ProcessFilterParam(TraktorZ1Side.Left,  0, cur[IdxGainL], _prev[IdxGainL]);
        ProcessFilterParam(TraktorZ1Side.Left,  1, cur[IdxHiL],   _prev[IdxHiL]);
        ProcessFilterParam(TraktorZ1Side.Left,  2, cur[IdxMidL],  _prev[IdxMidL]);
        ProcessFilterParam(TraktorZ1Side.Left,  3, cur[IdxLowL],  _prev[IdxLowL]);

        ProcessFilterParam(TraktorZ1Side.Right, 0, cur[IdxGainR], _prev[IdxGainR]);
        ProcessFilterParam(TraktorZ1Side.Right, 1, cur[IdxHiR],   _prev[IdxHiR]);
        ProcessFilterParam(TraktorZ1Side.Right, 2, cur[IdxMidR],  _prev[IdxMidR]);
        ProcessFilterParam(TraktorZ1Side.Right, 3, cur[IdxLowR],  _prev[IdxLowR]);

        if ((buttons & BtnOnL) != 0 && (_prevButtons & BtnOnL) == 0)
            CycleSection(TraktorZ1Side.Left, stream);
        if ((buttons & BtnOnR) != 0 && (_prevButtons & BtnOnR) == 0)
            CycleSection(TraktorZ1Side.Right, stream);

        Array.Copy(cur, _prev, 14);
        _prevButtons = buttons;
    }

    private void SetVolume(TraktorZ1Side side, ushort raw)
    {
        var node = _setup.GetMasterFaderNode(side);
        if (node is null) return;

        var normalized = (float)(raw / 65535.0);
        var sideIdx    = (int)side;

        if (_faderCatchingUp[sideIdx])
        {
            var known = GetCurrentVolumeNormalized(node);
            if (known is null || MathF.Abs(normalized - known.Value) > PickUpThreshold)
                return;
            _faderCatchingUp[sideIdx] = false;
        }

        var gains = Pan.GetGainsFromPanAndVolume(0.0, normalized);
        node.SetVolumes(Pan.BoostToExternal(gains));
    }

    private void ProcessFilterParam(TraktorZ1Side side, int paramIndex,
        ushort raw, ushort prev)
    {
        if (raw == prev) return;

        var sections   = _setup.GetSections(side);
        var sectionIdx = _selectedSection[(int)side];
        if (sectionIdx >= sections.Count) return;

        var section = sections[sectionIdx];
        if (paramIndex >= section.Count) return;

        var param      = section[paramIndex];
        var normalized = raw / 65535.0f;
        var sideIdx    = (int)side;

        if (_knobCatchingUp[sideIdx][paramIndex])
        {
            var known = GetCurrentParamNormalized(param.PluginNode, param.Name,
                param.Min, param.Max);
            if (known is null || MathF.Abs(normalized - known.Value) > PickUpThreshold)
                return;
            _knobCatchingUp[sideIdx][paramIndex] = false;
        }

        var actual = param.Min + normalized * (param.Max - param.Min);
        param.PluginNode.SetParam(param.Name, actual);
    }

    private void CycleSection(TraktorZ1Side side, FileStream stream)
    {
        var sections = _setup.GetSections(side);
        if (sections.Count == 0) return;

        _selectedSection[(int)side] =
            (_selectedSection[(int)side] + 1) % sections.Count;

        var sideIdx = (int)side;
        for (int i = 0; i < 4; i++)
            _knobCatchingUp[sideIdx][i] = true;

        FilterSectionChanged?.Invoke(side, _selectedSection[(int)side]);
        WriteLeds(stream);
    }

    private static float? GetCurrentVolumeNormalized(Node node)
    {
        if (!node.Properties.IsCompleted) return null;
        var props = node.Properties.Result;
        if (props is null || props.Channels.Count < 2) return null;

        var external  = props.Channels.Select(c => (double)c.Volume).ToArray();
        var internal_ = Pan.AttenuateFromExternal(external);
        var (_, volume) = Pan.GetPanAndVolumeFromGains(internal_[0], internal_[1]);
        return volume;
    }

    private static float? GetCurrentParamNormalized(Node node, string name,
        float min, float max)
    {
        if (!node.Params.IsCompleted) return null;
        var paramsDict = node.Params.Result;
        if (paramsDict is null || !paramsDict.TryGetValue(name, out var param))
            return null;
        if (param is not Parameter<float> floatParam) return null;
        if (max <= min) return null;
        return (floatParam.Value - min) / (max - min);
    }

    private void WriteLeds(FileStream stream)
    {
        var buf = new byte[22];
        buf[0]  = ReportIdOutput;
        buf[17] = 0xff; // on_button_l brightness
        buf[18] = 0x0f; // on_button_l color
        buf[20] = 0xff; // on_button_r brightness
        buf[21] = 0x0f; // on_button_r color

        try
        {
            stream.Write(buf, 0, buf.Length);
            stream.Flush();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to write LEDs: {Message}", ex.Message);
        }
    }
}
