using System.Collections.Immutable;

namespace MiddleweightReflection
{
    public interface IMrHasCustomAttributes : IMrNamedElement
    {
        ImmutableArray<MrCustomAttribute> GetCustomAttributes();
    }
}
