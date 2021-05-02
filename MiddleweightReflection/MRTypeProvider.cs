using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace MiddleweightReflection
{
    // Test implementation of ISignatureTypeProvider<TType, TGenericContext> that uses strings in ilasm syntax as TType.
    // A real provider in any sort of perf constraints would not want to allocate strings freely like this, but it keeps test code simple.
    internal class DisassemblingTypeProvider : ISignatureTypeProvider<MrType, MRGenericContext>
    {
        protected MrAssembly Assembly { get; }

        public DisassemblingTypeProvider(MrAssembly assembly)
        {
            Assembly = assembly;
        }

        public virtual MrType GetPrimitiveType(PrimitiveTypeCode typeCode)
        {
            return MrType.CreatePrimitiveType(typeCode);
        }


        public virtual MrType GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind = 0)
        {
            TypeDefinition definition = reader.GetTypeDefinition(handle);

            if (definition.IsNested)
            {
                // bugbug: For some reason, for nested types, the caller seems to expect
                // the outer type.

                TypeDefinitionHandle declaringTypeHandle = definition.GetDeclaringType();
                return GetTypeFromDefinition(reader, declaringTypeHandle, 0);  // + "/" + name;
            }

            return handle.AsMrType(Assembly);
        }

        public virtual MrType GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind = 0)
        {
            TypeReference reference = reader.GetTypeReference(handle);
            Handle scope = reference.ResolutionScope;

            string name = reference.Namespace.IsNil
                ? reader.GetString(reference.Name)
                : reader.GetString(reference.Namespace) + "." + reader.GetString(reference.Name);

            switch (scope.Kind)
            {
                // bugbug: consolidate with LoadContext.GetTypeFromReference?

                //case HandleKind.ModuleReference:
                //    return "[.module  " + _reader.GetString(_reader.GetModuleReference((ModuleReferenceHandle)scope).Name) + "]" + name;

                case HandleKind.AssemblyReference:
                    var assemblyReferenceHandle = (AssemblyReferenceHandle)scope;
                    var assemblyReference = reader.GetAssemblyReference(assemblyReferenceHandle);
                    //return "[" + _reader.GetString(assemblyReference.Name) + "]" + name;

                    return Assembly.LoadContext.GetTypeFromAssembly(name, assemblyReference.Name.AsString(Assembly));

                //MyDebug.WriteLine("[" + reader.GetString(assemblyReference.Name) + "]" + name);
                //MainWindow.UpdateTypeCache($@"{reader.GetString(assemblyReference.Name)}");
                //var wrapperT = new TypeDefinitionHandleWrapper() { IsAssembly = true };
                //return wrapperT;
                // bugbug: Assembly type?


                case HandleKind.TypeReference:
                    return GetTypeFromReference(this.Assembly.Reader, (TypeReferenceHandle)scope, rawTypeKind);

                case HandleKind.ModuleDefinition:
                    {
                        if (!Assembly.LoadContext.TryFindMrType(name, out var wrapper))
                            throw new Exception("Can't find wrapper");
                        return wrapper;
                    }


                default:
                    // rare cases:  ModuleDefinition means search within defs of current module (used by WinMDs for projections)
                    //              nil means search exported types of same module (haven't seen this in practice). For the test
                    //              purposes here, it's sufficient to format both like defs.
                    Debug.Assert(scope == Handle.ModuleDefinition || scope.IsNil);
                    throw new Exception("Not supported");
            }
        }

        public virtual MrType GetTypeFromSpecification(MetadataReader reader, MRGenericContext genericContext, TypeSpecificationHandle handle, byte rawTypeKind = 0)
        {
            return reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
        }

        public virtual MrType GetSZArrayType(MrType elementType)
        {
            if (elementType.IsGenericParameter)
            {
                return MrType.CreateFromGenericParameterHandle(
                    elementType.GenericParameterHandle.Value,
                    elementType.Assembly,
                    arrayRank: 1);
            }

            return MrType.Clone(elementType, arrayRankOverride: 1);
        }

        public virtual MrType GetPointerType(MrType elementType)
        {
            return MrType.Clone(elementType, isPointerOverride: true);
        }

        public virtual MrType GetByReferenceType(MrType elementType)
        {
            return MrType.Clone(
                elementType, 
                arrayRankOverride: elementType.ArrayRank, 
                isReferenceOverride: true);
        }

        public virtual MrType GetGenericMethodParameter(MRGenericContext genericContext, int index)
        {
            return genericContext.MethodParameters[index];
            //return "!!" + genericContext.MethodParameters[index];
        }

        // https://github.com/dotnet/corefx/blob/d3911035f2ba3eb5c44310342cc1d654e42aa316/src/System.Reflection.Metadata/tests/Metadata/Decoding/SignatureDecoderTests.cs
        public virtual MrType GetGenericTypeParameter(MRGenericContext genericContext, int index)
        {
            // Bugbug: Not sure why this happens. It shows up with
            // the Windows SDK NuGet file (the one generated by cs/winrt)
            if (genericContext == null)
                return null;

            return genericContext.TypeParameters[index];
            //return "!" + genericContext.TypeParameters[index];
        }

        public virtual MrType GetPinnedType(MrType elementType)
        {
            Debug.Assert(false);
            throw new Exception("Not supported");
            //return elementType + " pinned";
        }

        public virtual MrType GetGenericInstantiation(MrType genericType, ImmutableArray<MrType> typeArguments)
        {
            //return genericType + "<" + string.Join(",", typeArguments) + ">";

            // Clone the open type
            var newType = MrType.Clone(genericType);

            // Then set the type arguments. Note that the typeArguments might actually type parameters, so this 
            // won't neceessarily produce a closed type.
            newType.SetGenericArguments(typeArguments); 
            return newType;
        }

        public virtual MrType GetArrayType(MrType elementType, ArrayShape shape)
        {
            var newType = MrType.Clone(elementType, arrayRankOverride: shape.Rank);
            return newType;

            //var builder = new StringBuilder();

            //builder.Append(elementType);
            //builder.Append('[');

            //for (int i = 0; i < shape.Rank; i++)
            //{
            //    int lowerBound = 0;

            //    if (i < shape.LowerBounds.Length)
            //    {
            //        lowerBound = shape.LowerBounds[i];
            //        builder.Append(lowerBound);
            //    }

            //    builder.Append("...");

            //    if (i < shape.Sizes.Length)
            //    {
            //        builder.Append(lowerBound + shape.Sizes[i] - 1);
            //    }

            //    if (i < shape.Rank - 1)
            //    {
            //        builder.Append(',');
            //    }
            //}

            //builder.Append(']');
            //return builder.ToString();
        }

        public virtual MrType GetTypeFromHandle(MetadataReader _reader, MRGenericContext genericContext, EntityHandle handle)
        {
            Debug.Assert(false);
            throw new Exception("Not supported");
            //switch (handle.Kind)
            //{
            //    case HandleKind.TypeDefinition:
            //        return GetTypeFromDefinition(_reader, (TypeDefinitionHandle)handle);

            //    case HandleKind.TypeReference:
            //        return GetTypeFromReference(_reader, (TypeReferenceHandle)handle);

            //    case HandleKind.TypeSpecification:
            //        return GetTypeFromSpecification(_reader, genericContext, (TypeSpecificationHandle)handle);

            //    default:
            //        throw new ArgumentOutOfRangeException(nameof(handle));
            //}
        }

        public virtual MrType GetModifiedType(MrType modifierType, MrType unmodifiedType, bool isRequired)
        {
            var isConst = false;
            if (modifierType.GetFullName() == "System.Runtime.CompilerServices.IsConst")
            {
                isConst = true;
            }
            return MrType.Clone(unmodifiedType, isConstOverride: isConst);
        }


        public virtual MrType GetFunctionPointerType(MethodSignature<MrType> signature)
        {
            Debug.Assert(false);
            throw new NotSupportedException();
            //ImmutableArray<string> parameterTypes = signature.ParameterTypes;

            //int requiredParameterCount = signature.RequiredParameterCount;

            //var builder = new StringBuilder();
            //builder.Append("method ");
            //builder.Append(signature.ReturnType);
            //builder.Append(" *(");

            //int i;
            //for (i = 0; i < requiredParameterCount; i++)
            //{
            //    builder.Append(parameterTypes[i]);
            //    if (i < parameterTypes.Length - 1)
            //    {
            //        builder.Append(", ");
            //    }
            //}

            //if (i < parameterTypes.Length)
            //{
            //    builder.Append("..., ");
            //    for (; i < parameterTypes.Length; i++)
            //    {
            //        builder.Append(parameterTypes[i]);
            //        if (i < parameterTypes.Length - 1)
            //        {
            //            builder.Append(", ");
            //        }
            //    }
            //}

            //builder.Append(')');
            //return builder.ToString();
        }
    }
}
