using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace MiddleweightReflection
{
    /// <summary>
    /// An implementation of ICustomAttributeTypeProvider to provide MRTypes
    /// This is passed to System.Reflection.Metadata.CustomAttribute.DecodeValue()
    /// </summary>
    internal class CustomAttributeTypeProvider : DisassemblingTypeProvider, ICustomAttributeTypeProvider<MrType>
    {
        public CustomAttributeTypeProvider(MrAssembly assembly) : base(assembly)
        {
        }

        MrType ICustomAttributeTypeProvider<MrType>.GetSystemType()
        {
            if(this.Assembly.LoadContext.TryFindMrType("System.Type", out var type))
            {
                return type;
            }

            throw new Exception("Couldn't find System.Type in any loaded assembly");
        }

        bool ICustomAttributeTypeProvider<MrType>.IsSystemType(MrType type)
        {
            return type.GetFullName() == "System.Type";
        }

        MrType ICustomAttributeTypeProvider<MrType>.GetTypeFromSerializedName(string name)
        {
            if (Assembly.LoadContext.TryFindMrType(name, out var mrType))
            {
                return mrType;
            }

            // This is a type in an assembly that's not loaded, create a fake type.
            return MrType.CreateFakeType(name, assembly:null);
        }

        PrimitiveTypeCode ICustomAttributeTypeProvider<MrType>.GetUnderlyingEnumType(MrType type)
        {
            // Get the underlying type of an enum. The metadata reader needs this so that when it's
            // reading an attribute, it knows how many bytes to read out of the binary format.

            if(type.IsFakeType)
            {
                // This will be caught
                throw new MrException("Can't get the underlying type of a fake type");
            }
            Debug.Assert(type.IsEnum);

            var underlyingType = type.GetUnderlyingEnumType();
            Debug.Assert(underlyingType.IsTypeCode);

            return underlyingType.TypeCode;
        }
    }

}
