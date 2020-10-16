using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiddleweightReflection
{
    /// <summary>
    /// Used to provide generic type definitions to MetadataReader APIs, such as MethodDefinition.DecodeSignature
    /// </summary>
    internal class MRGenericContext
    {
        public MRGenericContext(ImmutableArray<MrType> typeParameters, ImmutableArray<MrType> methodParameters)
        {
            MethodParameters = methodParameters;
            TypeParameters = typeParameters;
        }

        public ImmutableArray<MrType> MethodParameters { get; }
        public ImmutableArray<MrType> TypeParameters { get; }
    }

}
