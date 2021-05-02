using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace MiddleweightReflection
{
    // Base class for MrType and all the Mr member classes, to improve polymorphism
    abstract public class MrTypeAndMemberBase
    {
        public abstract MrType DeclaringType { get; }

        public abstract ImmutableArray<MrCustomAttribute> GetCustomAttributes();

        public abstract string GetName();
    }
}
