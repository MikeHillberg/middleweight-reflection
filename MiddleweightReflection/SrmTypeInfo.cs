using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace MiddleweightReflection
{

#if false
    public class TypeDefinitionHandleWrapper
    {
        public TypeDefinitionHandle TypeDefinitionHandle { get; }
        public PrimitiveTypeCode TypeCode { get; }

        public TypeDefinition TypeDefinition { get; }
        MetadataReader _reader;
        public MetadataReader Reader { get; }

        public bool IsTypeCode;
        public bool IsArray { get; set; }
        public bool IsPointer { get; set; }
        public bool IsReference { get; set; }
        public bool IsAssembly { get; set; }
        public bool IsGeneric
        {
            get { return _typeArguments != null; }
        }

        public GenericParameterHandle? GenericParameterHandle { get; }
        public bool IsGenericParameter
        {
            get { return GenericParameterHandle != null; }
        }
        bool IsValid { get; } = true;

        public TypeDefinitionHandleWrapper()
        {
            //Debug.Assert(false);
            IsValid = false;
        }

        public TypeDefinitionHandleWrapper(MetadataReader reader, GenericParameterHandle handle)
        {
            Reader = reader;
            GenericParameterHandle = handle;
        }

        public TypeDefinitionHandleWrapper(MetadataReader reader, TypeDefinitionHandle handle)
        {
            Debug.Assert(reader != null);
            Debug.Assert(!handle.IsNil);

            Reader = reader;
            TypeDefinitionHandle = handle;
            TypeDefinition = reader.GetTypeDefinition(handle);
            IsTypeCode = false;
            TypeCode = PrimitiveTypeCode.Void;
        }

        public TypeDefinitionHandleWrapper(MetadataReader reader, PrimitiveTypeCode typeCode)
        {
            Debug.Assert(reader != null);

            Reader = reader;
            TypeCode = typeCode;
            IsTypeCode = true;
            TypeDefinitionHandle = new TypeDefinitionHandle();
        }

        public TypeDefinitionHandleWrapper(TypeDefinitionHandleWrapper other)
        {
            Reader = other.Reader;
            Debug.Assert(Reader != null);

            TypeCode = other.TypeCode;
            IsTypeCode = other.IsTypeCode;
            TypeDefinitionHandle = other.TypeDefinitionHandle;
            TypeDefinition = other.TypeDefinition;
            IsReference = other.IsReference;
            IsPointer = other.IsPointer;
            IsArray = other.IsArray;
            GenericParameterHandle = other.GenericParameterHandle;
        }

        IEnumerable<TypeDefinitionHandleWrapper> _typeArguments;
        public void SetGenericArguments(IEnumerable<TypeDefinitionHandleWrapper> typeArguments)
        {
            _typeArguments = typeArguments;
        }

        public override string ToString()
        {
            return FullName;
        }

        public string FullName
        {
            get
            {
                string name = "";

                if (IsAssembly)
                {
                    return "(Assembly)";
                }

                if (IsTypeCode)
                    name = TypeCodeToName(TypeCode);
                else if (IsGenericParameter)
                {
                    name = $"{Reader.GetString(Reader.GetGenericParameter(GenericParameterHandle.Value).Name)}";
                }
                else if (Reader != null)
                {
                    //var typeDefinition = Reader.GetTypeDefinition(TypeDefinitionHandle);

                    name = GetTypeNameFromTypeDefinition(Reader, TypeDefinition);
                }
                else
                    name = "(unknown)";

                if (IsArray)
                {
                    name = name + "[]";
                }
                if (IsReference)
                {
                    name = name + "&";
                }
                if (IsGeneric)
                {
                    var lastIndex = name.LastIndexOf('`');
                    if (lastIndex != -1)
                    {
                        name = name.Substring(0, lastIndex);
                    }
                    name = name + "<" + string.Join(",", _typeArguments) + ">";
                }
                if (IsPointer)
                {
                    name = name + "*";
                }

                return name;
            }
        }

        static public string GetTypeNameFromTypeDefinition(MetadataReader reader, TypeDefinition typeDefinition)
        {
            var typeNameBuilder = new StringBuilder();
            if (typeDefinition.IsNested)
            {
                var declaringTypeDefinitionHandle = typeDefinition.GetDeclaringType();
                var declaringTypeDefinition = reader.GetTypeDefinition(declaringTypeDefinitionHandle);
                typeNameBuilder.Append(GetTypeNameFromTypeDefinition(reader, declaringTypeDefinition));
                typeNameBuilder.Append("+");
            }
            else
            {
                var ns = reader.GetString(typeDefinition.Namespace);
                if (!string.IsNullOrEmpty(ns))
                {
                    typeNameBuilder.Append(reader.GetString(typeDefinition.Namespace));
                    typeNameBuilder.Append(".");
                }
            }
            typeNameBuilder.Append(reader.GetString(typeDefinition.Name));

            return typeNameBuilder.ToString();
        }

        public string DebugString { get; internal set; }

        public string TypeCodeToName(PrimitiveTypeCode typeCode)
        {
            switch (typeCode)
            {
                case PrimitiveTypeCode.Boolean:
                    return "bool";

                case PrimitiveTypeCode.Byte:
                    return "uint8";

                case PrimitiveTypeCode.Char:
                    return "char";

                case PrimitiveTypeCode.Double:
                    return "float64";

                case PrimitiveTypeCode.Int16:
                    return "int16";

                case PrimitiveTypeCode.Int32:
                    return "int32";

                case PrimitiveTypeCode.Int64:
                    return "int64";

                case PrimitiveTypeCode.IntPtr:
                    return "native int";

                case PrimitiveTypeCode.Object:
                    return "object";

                case PrimitiveTypeCode.SByte:
                    return "int8";

                case PrimitiveTypeCode.Single:
                    return "float32";

                case PrimitiveTypeCode.String:
                    return "string";

                case PrimitiveTypeCode.TypedReference:
                    return "typedref";

                case PrimitiveTypeCode.UInt16:
                    return "uint16";

                case PrimitiveTypeCode.UInt32:
                    return "uint32";

                case PrimitiveTypeCode.UInt64:
                    return "uint64";

                case PrimitiveTypeCode.UIntPtr:
                    return "native uint";

                case PrimitiveTypeCode.Void:
                    return "void";

                default:
                    Debug.Assert(false);
                    throw new ArgumentOutOfRangeException(nameof(typeCode));
            }
        }
    }

    static public class TypeDefinitionHandleWrapperExtensions
    {
        static public TypeDefinitionHandleWrapper AsWrapper(this TypeDefinitionHandle handle, MetadataReader reader)
        {
            return new TypeDefinitionHandleWrapper(reader, handle);
        }

        static public TypeDefinitionHandleWrapper AsWrapper(this PrimitiveTypeCode typeCode, MetadataReader reader)
        {
            return new TypeDefinitionHandleWrapper(reader, typeCode);
        }

    }


#endif

}
