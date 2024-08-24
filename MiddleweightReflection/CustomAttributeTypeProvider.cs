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
            if (this.Assembly.LoadContext.TryFindMrType("System.Type", out var type))
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
            return MrType.CreateFakeType(name, assembly: null);
        }

        PrimitiveTypeCode ICustomAttributeTypeProvider<MrType>.GetUnderlyingEnumType(MrType type)
        {
            // Get the underlying type of an enum. The metadata reader needs this so that when it's
            // reading an attribute, it knows how many bytes to read out of the binary format.

            if (type.IsFakeType)
            {
                // In general if there's a referenced type that we don't have loaded, we fake it up.
                // But for a fake enum type, the Reader really needs to know what size it is in order to read
                // the binary data.
                // Maybe we should pre-load some well-known .Net assemblies to handle this.
                // But for now, just hard-coding a couple of frequent types.

                const string callingConvention = "System.Runtime.InteropServices.CallingConvention";
                var typeName = type.GetPrettyFullName();
                if(typeName == callingConvention)
                {
                   return PrimitiveTypeCode.Int32;
                }

                const string attributeTargets = "System.AttributeTargets";
                if(typeName == attributeTargets)
                {
                    // [Flags] are unsigned
                    return PrimitiveTypeCode.UInt32;
                }

                var message = $"Can't get the underlying type of a fake type ({type.GetPrettyFullName()})";
                Debug.WriteLine(message);

                // We're in an MR stack frame now, above some SMR frames, and then MR
                // So we're throwing from MR through SMR to an MR catch block.
                throw new MrException(message);
            }
            Debug.Assert(type.IsEnum);

            var underlyingType = type.GetUnderlyingEnumType();
            Debug.Assert(underlyingType.IsTypeCode);

            return underlyingType.TypeCode;
        }
    }

}
