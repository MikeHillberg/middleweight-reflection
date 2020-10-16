using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiddleweightReflection
{
    /// <summary>
    /// A parsed version of MethodDefinition.Attributes, with properties instead of non-trivial bit flag math.
    /// </summary>
    public struct ParsedMethodAttributes
    {
        public bool IsStatic;
        public bool IsPublic;
        public bool IsPrivate;
        public bool IsInternal;
        public bool IsVirtual;
        public bool IsProtected;
        public bool IsOverride;
        public bool IsSealed;
        public bool IsAbstract;
    }
}
