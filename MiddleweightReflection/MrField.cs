using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;

namespace MiddleweightReflection
{
    public class MrField : IMrHasCustomAttributes, IMrTypeMember
    {
        public MrType DeclaringType { get; }
        public FieldDefinitionHandle DefinitionHandle { get; }
        public FieldDefinition Definition { get; }

        private MrField(
            FieldDefinitionHandle fieldDefinitionHandle, 
            FieldDefinition fieldDefinition,
            MrType declaringType)
        {
            DeclaringType = declaringType;
            DefinitionHandle = fieldDefinitionHandle;
            Definition = fieldDefinition;
        }

        static internal MrField TryCreate(
            FieldDefinitionHandle fieldDefinitionHandle, 
            MrType declaringType,
            bool publicishOnly)
        {
            var fieldDefinition = declaringType.Assembly.Reader.GetFieldDefinition(fieldDefinitionHandle);
            if(!publicishOnly || AreFieldAttributesPublicish(fieldDefinition.Attributes, declaringType))
            {
                return new MrField(fieldDefinitionHandle, fieldDefinition, declaringType);
            }
            return null;
        }

        public override string ToString()
        {
            return $"{this.DeclaringType.GetPrettyName()}.{GetName()}";
        }

        public bool IsPrivate
        {
            get
            {
                var attributes = Definition.Attributes;
                return (attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Private;
            }
        }

        public MrType GetFieldType()
        {
            return this.Definition.DecodeSignature(this.DeclaringType.Assembly.TypeProvider, null);
        }

        public string GetName()
        {
            return Definition.Name.AsString(DeclaringType.Assembly);
        }

        public object GetConstantValue(out ConstantTypeCode constantTypeCode)
        {
            var defaultValueConstantHandle = this.Definition.GetDefaultValue();
            if(defaultValueConstantHandle.IsNil)
            {
                // bugbug: happens with winRT projection turned on
                constantTypeCode = ConstantTypeCode.Invalid;
                return null;
            }

            // bugbug: does this work for anything other than enum?

            var defaultValueConstant = this.DeclaringType.Assembly.Reader.GetConstant(defaultValueConstantHandle);
            var valueBlob = defaultValueConstant.Value;
            var blobReader = this.DeclaringType.Assembly.Reader.GetBlobReader(valueBlob);

            constantTypeCode = defaultValueConstant.TypeCode;
            switch(constantTypeCode)
            {
                case ConstantTypeCode.Int32:
                    return blobReader.ReadInt32();

                case ConstantTypeCode.Int16:
                    return blobReader.ReadInt16();

                case ConstantTypeCode.UInt32:
                    return (int)blobReader.ReadUInt32();

                case ConstantTypeCode.UInt16:
                    return (int)blobReader.ReadUInt16();

                case ConstantTypeCode.Byte:
                    return (int)blobReader.ReadByte();

                default:
                    throw new NotSupportedException("Unsupported ContentTypeCode");
            }
        }

        public bool TryGetDefaultValue(out object value, out ConstantTypeCode typeCode)
        {
            typeCode = ConstantTypeCode.Invalid;
            value = null;

            var defaultValueConstantHandle = this.Definition.GetDefaultValue();
            if (defaultValueConstantHandle.IsNil)
            {
                // bugbug: happens with winRT projection turned on
                return false;
            }

            var constant = this.DeclaringType.Assembly.Reader.GetConstant(defaultValueConstantHandle);
            if(constant.TypeCode != ConstantTypeCode.Invalid)
            {
                value = constant.Value;
                typeCode = constant.TypeCode;
                return true;
            }

            return false;
        }

        public bool IsSpecialName
        {
            get
            {
                var attributes = this.Definition.Attributes;
                return attributes.HasFlag(FieldAttributes.SpecialName) || attributes.HasFlag(FieldAttributes.RTSpecialName);
            }
        }

        public ImmutableArray<MrCustomAttribute> GetCustomAttributes()
        {
            var customAttributeHandles = this.Definition.GetCustomAttributes();
            var customAttributes = MrAssembly.GetCustomAttributesFromHandles(customAttributeHandles, this.DeclaringType);
            return customAttributes.ToImmutableArray();
        }

        static internal bool IsPublicFieldAttributes(FieldAttributes attributes)
        {
            return (attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Public;
        }

        static internal bool IsPrivateFieldAttributes(FieldAttributes attributes)
        {
            return (attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Private;
        }

        static internal bool IsInternalFieldAttributes(FieldAttributes attributes)
        {
            return (attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Assembly;
        }

        internal static bool IsStaticFieldAttributes(FieldAttributes attributes)
        {
            return attributes.HasFlag(FieldAttributes.Static);
        }
        static internal bool IsProtectedFieldAttributes(FieldAttributes attributes)
        {
            return
                attributes.HasFlag(FieldAttributes.Family)
                || attributes.HasFlag(FieldAttributes.FamANDAssem)
                || attributes.HasFlag(FieldAttributes.FamORAssem) && !attributes.HasFlag(MethodAttributes.Assembly);
        }

        public ParsedFieldAttributes GetFieldModifiers()
        {
            var attributes = this.Definition.Attributes;

            var modifiers = new ParsedFieldAttributes();

            modifiers.IsPublic = IsPublicFieldAttributes(attributes);
            modifiers.IsPrivate = IsPrivateFieldAttributes(attributes);
            modifiers.IsInternal = IsInternalFieldAttributes(attributes);
            modifiers.IsStatic = IsStaticFieldAttributes(attributes);
            modifiers.IsProtected = IsProtectedFieldAttributes(attributes);
            modifiers.IsConst = attributes.HasFlag(FieldAttributes.Static) && attributes.HasFlag(FieldAttributes.Literal);

            return modifiers;
        }

        static bool AreFieldAttributesPublicish(FieldAttributes attributes, MrType declaringType)
        {
            if (IsPublicFieldAttributes(attributes) 
                || IsProtectedFieldAttributes(attributes)
                    && !declaringType.IsSealed)
            {
                return true;
            }

            return false;
        }
    }
}
