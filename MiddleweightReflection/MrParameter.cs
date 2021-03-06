﻿using System;
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
    }
}
