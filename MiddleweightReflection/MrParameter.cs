using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace MiddleweightReflection
{
    /// <summary>
    /// A parameter for a MRMethod
    /// </summary>
    public class MrParameter
    {
        public MrMethod Method { get; }

        ParameterHandle _parameterHandle;
        Parameter _parameter;
        int _parameterIndex;

        public MrParameter(MrMethod method, ParameterHandle parameterHandle, int parameterIndex)
        {
            _parameterHandle = parameterHandle;
            Method = method;
            _parameterIndex = parameterIndex;

            _parameter = Method.DeclaringType.Assembly.Reader.GetParameter(_parameterHandle);
        }

        public override bool Equals(object obj)
        {
            var other = obj as MrParameter;
            var prolog = MrLoadContext.OverrideEqualsProlog(this, other);
            if (prolog != null)
            {
                return (bool)prolog;
            }

            var matches = 
                this.Method == other.Method
                && this._parameterHandle == other._parameterHandle;

            return matches;
        }

        public static bool operator ==(MrParameter operand1, MrParameter operand2)
        {
            return MrLoadContext.OperatorEquals(operand1, operand2);
        }

        public static bool operator !=(MrParameter operand1, MrParameter operand2)
        {
            return !(operand1 == operand2);
        }

        public override int GetHashCode()
        {
            return this._parameterHandle.GetHashCode();
        }

        /// <summary>
        /// The type of the parameter, which might be a generic type parameter (e.g. the T in List of T)
        /// </summary>
        /// <returns></returns>
        public MrType GetParameterType()
        {
            var parameterType = Method.MethodSignature.ParameterTypes[_parameterIndex];

            if(parameterType.IsGenericParameter)
            {
                // E.g. this parameterType is the item's T for List<T>.Add(T item)
                // We want to get the method's type argument that correponds to this type parameter

                // Get the parameter type's unmodified name, as a string.
                // E.g. for the baz in Foo<T>.Bar(T& baz), return "T"
                var parameterTypeName = MrType.GetUnmodifiedTypeName(
                                                    parameterType.GetName(), 
                                                    out var isArray, out var isReference, out var isPointer);

                // Find the method type argument with the same name as this parameter type.
                var typeArguments = this.Method.DeclaringType.GetGenericArguments();
                foreach(var typeArgument in typeArguments)
                {
                    if(typeArgument.TypeParameterName == parameterTypeName)
                    {
                        // Return the type argument, correctly modified ("T&" rather than "T")
                        parameterType = MrType.Clone(typeArgument, isArray, isReference, isPointer);
                        break;
                    }
                }
            }

            return parameterType;
        }




        /// <summary>
        /// The parameter's name (not the parameter's type)
        /// </summary>
        public string GetParameterName()
        {
            var parameter = Method.DeclaringType.Assembly.Reader.GetParameter(_parameterHandle);
            return parameter.Name.AsString(Method.DeclaringType.Assembly);
        }

        public ParameterAttributes Attributes => _parameter.Attributes;

        /// <summary>
        /// Returns true if this parameter has a default value.
        /// </summary>
        public bool HasDefaultValue
        {
            get
            {
                return _parameter.Attributes.HasFlag(ParameterAttributes.HasDefault)
                    && !_parameter.GetDefaultValue().IsNil;
            }
        }

        /// <summary>
        /// Gets the default value of this parameter, or null if none.
        /// Returns the value as a boxed object (int, string, bool, etc.).
        /// </summary>
        public object GetDefaultValue()
        {
            var constantHandle = _parameter.GetDefaultValue();
            if (constantHandle.IsNil)
            {
                return null;
            }

            var reader = Method.DeclaringType.Assembly.Reader;
            var constant = reader.GetConstant(constantHandle);
            var blobReader = reader.GetBlobReader(constant.Value);

            switch (constant.TypeCode)
            {
                case ConstantTypeCode.Boolean:
                    return blobReader.ReadBoolean();
                case ConstantTypeCode.Char:
                    return blobReader.ReadChar();
                case ConstantTypeCode.SByte:
                    return blobReader.ReadSByte();
                case ConstantTypeCode.Byte:
                    return blobReader.ReadByte();
                case ConstantTypeCode.Int16:
                    return blobReader.ReadInt16();
                case ConstantTypeCode.UInt16:
                    return blobReader.ReadUInt16();
                case ConstantTypeCode.Int32:
                    return blobReader.ReadInt32();
                case ConstantTypeCode.UInt32:
                    return blobReader.ReadUInt32();
                case ConstantTypeCode.Int64:
                    return blobReader.ReadInt64();
                case ConstantTypeCode.UInt64:
                    return blobReader.ReadUInt64();
                case ConstantTypeCode.Single:
                    return blobReader.ReadSingle();
                case ConstantTypeCode.Double:
                    return blobReader.ReadDouble();
                case ConstantTypeCode.String:
                    return blobReader.ReadUTF16(blobReader.Length);
                case ConstantTypeCode.NullReference:
                    return null;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Gets the ConstantTypeCode for the default value, or Invalid if no default.
        /// </summary>
        public ConstantTypeCode DefaultValueTypeCode
        {
            get
            {
                var constantHandle = _parameter.GetDefaultValue();
                if (constantHandle.IsNil)
                {
                    return ConstantTypeCode.Invalid;
                }

                var reader = Method.DeclaringType.Assembly.Reader;
                return reader.GetConstant(constantHandle).TypeCode;
            }
        }
    }
}
