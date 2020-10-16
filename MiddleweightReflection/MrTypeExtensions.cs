using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace MiddleweightReflection
{
    static internal class MrTypeExtensions
    {
        static public MrType AsMrType(this TypeDefinitionHandle handle, MrAssembly assembly)
        {
            return MrType.CreateFromTypeDefinition(handle, assembly);
        }

        static public MrType AsMrType(this PrimitiveTypeCode typeCode, MrAssembly assembly)
        {
            return MrType.CreatePrimitiveType(typeCode);
        }

    }

}
