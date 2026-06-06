using System;
using Fr.Sonic.Model.Objects;
using Fr.Sonic.Modules.Models;
using SonicEddy.Contracts.FilterGraph;

namespace SonicEddy.Services.MixerServiceV2;

public abstract class InsertProcessor
{
    public abstract Node InputNode { get; }
    public abstract Node OutputNode { get; }
    public virtual FilterChain? FilterChain => null;
    public virtual FilterGraph? FilterGraph => null;
    public virtual Guid? ExternalEffectId => null;
    public abstract void Destroy();
}

public sealed class FilterChainInsertProcessor : InsertProcessor
{
    public FilterChainInsertProcessor(FilterChain filterChain,
        FilterGraph filterGraph)
    {
        FilterChain = filterChain;
        FilterGraph = filterGraph;
    }

    public override Node InputNode => FilterChain.CaptureNode;
    public override Node OutputNode => FilterChain.PlaybackNode;
    public override FilterChain FilterChain { get; }
    public override FilterGraph FilterGraph { get; }
    public override void Destroy() => FilterChain.Destroy();
}

public sealed class ExternalEffectInsertProcessor : InsertProcessor
{
    private readonly Action _release;

    public ExternalEffectInsertProcessor(Guid effectId,
        LoopbackModule inputLoopback, LoopbackModule outputLoopback,
        Action release)
    {
        ExternalEffectId = effectId;
        InputLoopback = inputLoopback;
        OutputLoopback = outputLoopback;
        _release = release;
    }

    public LoopbackModule InputLoopback { get; }
    public LoopbackModule OutputLoopback { get; }
    public override Guid? ExternalEffectId { get; }
    public override Node InputNode => InputLoopback.CaptureNode;
    public override Node OutputNode => OutputLoopback.PlaybackNode;

    public override void Destroy()
    {
        try
        {
            InputLoopback.Destroy();
        }
        finally
        {
            try
            {
                OutputLoopback.Destroy();
            }
            finally
            {
                _release();
            }
        }
    }
}

public abstract record InsertProcessorRequest;

public sealed record FilterChainInsertRequest(FilterGraph FilterGraph)
    : InsertProcessorRequest;

public sealed record ExternalEffectInsertRequest(Guid ExternalEffectId)
    : InsertProcessorRequest;
