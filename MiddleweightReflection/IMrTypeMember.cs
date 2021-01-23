namespace MiddleweightReflection
{
    public interface IMrTypeMember : IMrNamedElement
    {
        MrType DeclaringType { get; }
    }
}