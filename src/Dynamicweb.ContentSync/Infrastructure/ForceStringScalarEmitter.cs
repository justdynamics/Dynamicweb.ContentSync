using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.EventEmitters;

namespace Dynamicweb.ContentSync.Infrastructure;

public class ForceStringScalarEmitter : ChainedEventEmitter
{
    public ForceStringScalarEmitter(IEventEmitter next) : base(next) { }

    public override void Emit(ScalarEventInfo eventInfo, IEmitter emitter)
    {
        if (eventInfo.Source.Type == typeof(string) && eventInfo.Source.Value is string value)
        {
            if (value.Contains('\n') || value.Contains('\r'))
                eventInfo.Style = ScalarStyle.Literal;
            else
                eventInfo.Style = ScalarStyle.DoubleQuoted;
        }
        base.Emit(eventInfo, emitter);
    }
}
