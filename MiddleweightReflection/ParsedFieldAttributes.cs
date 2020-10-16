using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiddleweightReflection
{
    /// <summary>
    /// A parsed version of FieldAttributes, with properties instead of non-trivial bit flag combinations.
    /// </summary>
    public struct ParsedFieldAttributes
    {
        public bool IsStatic;
        public bool IsPublic;
        public bool IsPrivate;
        public bool IsInternal;
        public bool IsProtected;

        public bool IsConst { get; internal set; }
    }
}
