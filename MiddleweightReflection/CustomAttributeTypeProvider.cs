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

            if(name.Contains(","))
            {
                // bugbug: only support unqualified assembly names currently
                // So e.g. with "System.Windows.Modifiability, PresentationCore, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35",
                // pull out "System.Windows.Modifiability"
                // (This shows up in the Microsoft.WindowsDesktop.App .Net SDK)
                name = name.Split(',')[0];
            }

            // This is a type in an assembly that's not loaded, create a fake type.
            return MrType.CreateFakeType(name, assembly: null);
        }

        readonly string[] _nonFlagsAttributes = new string[]
        {
                            "System.Runtime.InteropServices.CallingConvention",
                            "System.ComponentModel.EditorBrowsableState",
                            "System.Windows.Modifiability",
                            "System.ComponentModel.DesignerSerializationVisibility",
                            "System.ComponentModel.RefreshProperties",
                            "System.Drawing.ContentAlignment",
                            "System.Environment+SpecialFolder",
                            "System.Runtime.InteropServices.ClassInterfaceType",
                            "System.Runtime.InteropServices.ComInterfaceType",
                            "System.Runtime.InteropServices.Marshalling.MarshalMode",
                            "System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes",
                            "System.StringComparison",
                            "System.Windows.Media.Animation.HandoffBehavior",
                            "System.Windows.Media.Animation.TimeSeekOrigin",
                            "System.Windows.Readability",
                            "Windows.Foundation.Metadata.ThreadingModel",
                            "Windows.Foundation.Metadata.DeprecationType",
                            "Windows.Foundation.Metadata.MarshalingType"
        };

        readonly string[] _flagsAttributes = new string[]
        {
                    "System.Windows.Modifiability",
                    "System.AttributeTargets",
                    "System.Runtime.InteropServices.TypeLibTypeFlags",
        };


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

                var typeName = type.GetPrettyFullName();

                // Handle some hard-coded types in .Net
                // First check for the non-flags types
                if (_nonFlagsAttributes.Contains(typeName))
                {
                    return PrimitiveTypeCode.Int32;
                }

                // And then the [flags] types  
                if (_flagsAttributes.Contains(typeName))
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
