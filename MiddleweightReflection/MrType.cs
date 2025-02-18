﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

namespace MiddleweightReflection
{
    /// <summary>
    /// Represents a type from an assembly (get this from an MRAssembly class)
    /// </summary>
    public class MrType : MrTypeAndMemberBase
    {
        //
        // Following is the state of the class
        // Any updates here should be matched in Clone and Equals
        //

        internal TypeDefinitionHandle TypeDefinitionHandle { get; }
        internal TypeDefinition TypeDefinition { get; }
        public MrAssembly Assembly { get; }

        // These are set for types that are in a referenced by not loaded assembly
        public string _fakeName;
        public string _fakeNamespace;

        public bool IsTypeCode { get; private set; }
        public PrimitiveTypeCode TypeCode { get; }

        public bool IsArray => ArrayRank != 0;

        public int ArrayRank { get; private set; } = 0;

        public bool IsPointer { get; private set; }
        public bool IsReference { get; private set; }
        public bool IsConst { get; private set; }

        public bool IsAssembly { get; private set; }

        // If not null, this indicates that this type is a generic parameter, e.g. the T in List of T
        public GenericParameterHandle? GenericParameterHandle { get; }

        ImmutableArray<MrType> _typeArguments;

        //
        // End of state
        //



        public bool IsGenericParameter
        {
            get { return GenericParameterHandle != null; }
        }


        public override MrType DeclaringType => this;

        public TypeAttributes Attributes
        {
            get
            {
                if (IsFakeType || IsFunctionPointer)
                {
                    return TypeAttributes.Public;
                }
                else if (IsTypeCode)
                {
                    // Is it necessary to handle each type separately?
                    return typeof(int).Attributes;
                }

                return TypeDefinition.Attributes;
            }
        }


        internal static MrType CreateFromGenericParameterHandle(
            GenericParameterHandle handle,
            MrAssembly assembly,
            int arrayRank = 0,
            bool isReference = false,
            bool isPointer = false)
        {
            return new MrType(handle, assembly)
            {
                ArrayRank = arrayRank,
                IsReference = isReference,
                IsPointer = isPointer
            };
        }

        // Create a new type from an old type, but modified to be an array/reference/pointer/const
        internal static MrType Clone(
            MrType other,
            int? arrayRankOverride = null,
            bool? isReferenceOverride = null,
            bool? isPointerOverride = null,
            bool? isConstOverride = null)
        {
            return new MrType(other, arrayRankOverride, isReferenceOverride, isPointerOverride, isConstOverride);
        }

        private MrType(
            MrType other,
            int? arrayRankOverride = null,
            bool? isReferenceOverride = null,
            bool? isPointerOverride = null,
            bool? isConstOverride = null)
        {
            Assembly = other.Assembly;

            TypeCode = other.TypeCode;
            IsTypeCode = other.IsTypeCode;
            TypeDefinitionHandle = other.TypeDefinitionHandle;
            TypeDefinition = other.TypeDefinition;
            GenericParameterHandle = other.GenericParameterHandle;
            _fakeName = other._fakeName;
            _fakeNamespace = other._fakeNamespace;
            _typeArguments = other._typeArguments;

            if (isPointerOverride == null)
            {
                IsPointer = other.IsPointer;
            }
            else
            {
                IsPointer = (bool)isPointerOverride;
            }

            if (isConstOverride == null)
            {
                IsConst = other.IsConst;
            }
            else
            {
                IsConst = (bool)isConstOverride;
            }

            if (isReferenceOverride == null)
            {
                IsReference = other.IsReference;
            }
            else
            {
                IsReference = (bool)isReferenceOverride;
            }

            if (arrayRankOverride == null)
            {
                ArrayRank = other.ArrayRank;
            }
            else
            {
                ArrayRank = (int)arrayRankOverride;
            }
        }

        public override bool Equals(object obj)
        {
            var other = obj as MrType;
            var prolog = MrLoadContext.OverrideEqualsProlog(this, other);
            if (prolog != null)
            {
                return (bool)prolog;
            }

            // Skip TypeDefinition because it doesn't support Equals, and it's generated from the
            // TypeDefinitionHandle anyway
            bool matches =
                this.Assembly == other.Assembly
                && this.TypeCode == other.TypeCode
                && this.IsArray == other.IsArray
                && this.IsReference == other.IsReference
                && this.IsPointer == other.IsPointer
                && this.IsConst == other.IsConst
                && this.IsTypeCode == other.IsTypeCode
                && this.TypeDefinitionHandle == other.TypeDefinitionHandle
                && this.GenericParameterHandle == other.GenericParameterHandle
                && this._fakeName == other._fakeName
                && this._fakeNamespace == other._fakeNamespace;

            if (!matches)
            {
                return false;
            }

            if (this.IsFunctionPointer)
            {

                var fp1 = this._functionPointerSignature;
                var fp2 = other._functionPointerSignature;

                if (fp1.Header != fp2.Header
                    || fp1.ReturnType != fp2.ReturnType
                    || fp1.GenericParameterCount != fp2.GenericParameterCount
                    || fp1.RequiredParameterCount != fp2.RequiredParameterCount)
                {
                    return false;
                }

                var parmTypes1 = fp1.ParameterTypes;
                var parmTypes2 = fp2.ParameterTypes;
                if (parmTypes1.Length != parmTypes2.Length)
                {
                    return false;
                }

                for(int i = 0; i < parmTypes1.Length; i++)
                {
                    if (parmTypes1[i] != parmTypes1[i])
                    {
                        return false;
                    }    
                }

                return true;
            }


            // This matches so far, but could be an IFoo<T> and an IFoo<string>
            // First check if this is at least partially closed
            // (some of _typeArguments may actually still be parameters)
            if (this._typeArguments != null || other._typeArguments != null)
            {
                if (this._typeArguments == null || other._typeArguments == null)
                {
                    // One has at least one type argument, the other doesn't (still full open)
                    return false;
                }

                if (this._typeArguments.Length != other._typeArguments.Length)
                {
                    return false;
                }

                // Compare each of the arguments
                for (int i = 0; i < this._typeArguments.Count(); i++)
                {
                    if (_typeArguments[i] != other._typeArguments[i])
                    {
                        // E.g. TypedEventHandler<T,A> closed with different type arguments
                        return false;
                    }
                }
            }

            /* Removed this because it's not necessary; the TypeDefinitionHandle will be different in this case
            
            // Above handled where this type is at least partially closed.
            // Now check if we're fully open
            else if(this.GetHasGenericParameters())
            {
                if(!other.GetHasGenericParameters())
                {
                    return false;
                }

                var thisGenericParameters = this.TypeDefinition.GetGenericParameters();
                var otherGenericParameters = other.TypeDefinition.GetGenericParameters();

                if(thisGenericParameters.Count != otherGenericParameters.Count)
                {
                    return false;
                }

                for (int i = 0; i < thisGenericParameters.Count; i++)
                {
                    if (thisGenericParameters[i] != otherGenericParameters[i])
                    {
                        return false;
                    }
                }
            }
            */

            return true;
        }

        public static bool operator ==(MrType operand1, MrType operand2)
        {
            return MrLoadContext.OperatorEquals(operand1, operand2);
        }

        public static bool operator !=(MrType operand1, MrType operand2)
        {
            return !(operand1 == operand2);
        }

        public override int GetHashCode()
        {
            var code = TypeDefinitionHandle.GetHashCode();
            code += TypeCode.GetHashCode();
            if (_fakeName != null)
            {
                code += _fakeName.GetHashCode();
            }

            if (this.GenericParameterHandle != null)
            {
                code += GenericParameterHandle.GetHashCode();
            }

            return code;
        }

        /// <summary>
        /// Create a primitive/fundamental type (int, float, string, etc)
        /// </summary>
        static Dictionary<PrimitiveTypeCode, MrType> _primitiveTypes = new Dictionary<PrimitiveTypeCode, MrType>();
        static internal MrType CreatePrimitiveType(PrimitiveTypeCode typeCode)
        {
            if (_primitiveTypes.TryGetValue(typeCode, out var type))
                return type;

            lock (_primitiveTypes)
            {
                if (_primitiveTypes.TryGetValue(typeCode, out type))
                    return type;

                type = new MrType(typeCode);
                _primitiveTypes[typeCode] = type;
                return type;
            }
        }
        private MrType(PrimitiveTypeCode typeCode)
        {
            TypeCode = typeCode;
            IsTypeCode = true;
        }

        private MrType(GenericParameterHandle handle, MrAssembly assembly)
        {
            Assembly = assembly;
            GenericParameterHandle = handle;
        }

        private MrType(string fakeFullName, MrAssembly assembly)
        {
            this.Assembly = assembly;

            var index = fakeFullName.LastIndexOf('.');
            if (index == -1)
            {
                _fakeName = fakeFullName;
            }
            else
            {
                if (fakeFullName.Contains(","))
                {
                    int j = 1132;
                }
                _fakeNamespace = fakeFullName.Substring(0, index);
                _fakeName = fakeFullName.Substring(index + 1);

            }

            Debug.WriteLine($"Faking {_fakeName}");
        }

        /// <summary>
        /// Create a type from the type table.
        /// </summary>
        static internal MrType CreateFromTypeDefinition(TypeDefinitionHandle typeDefinitionHandle, MrAssembly assembly)
        {
            var type = assembly.GetFromCacheOrCreate(
                                    typeDefinitionHandle,
                                    () => new MrType(typeDefinitionHandle, assembly));

            Debug.Assert(type != null);
            Debug.Assert(type == assembly.GetFromCacheOrCreate(typeDefinitionHandle, null));

            return type;
        }

        private MrType(TypeDefinitionHandle typeDefinitionHandle, MrAssembly assembly)
        {
            TypeDefinitionHandle = typeDefinitionHandle;
            Assembly = assembly;
            TypeDefinition = Assembly.Reader.GetTypeDefinition(typeDefinitionHandle);
        }

        // Create a placeholder type that was referenced but wasn't in any of the loaded assemblies
        static internal MrType CreateFakeType(string name, MrAssembly assembly)
        {
            return new MrType(name, assembly);
        }

        public bool IsFakeType => _fakeName != null;

        static internal MrType CreateFunctionPointerType(MethodSignature<MrType> signature)
        {
            return new MrType(signature);
        }

        // Function Pointer type
        private MrType(MethodSignature<MrType> signature)
        {
            _isFunctionPointer = true;
            _functionPointerSignature = signature;
        }
        bool _isFunctionPointer = false;
        MethodSignature<MrType> _functionPointerSignature;


        public bool IsFunctionPointer => _isFunctionPointer;

        public MrType GetFunctionPointerReturnType()
        {
            if (!IsFunctionPointer)
            {
                throw new InvalidOperationException("Not a function pointer");
            }

            return _functionPointerSignature.ReturnType;
        }

        /// <summary>
        /// A modified type is an array, pointer, or reference type.
        /// </summary>
        public bool IsModifiedType => IsArray || IsPointer || IsReference;

        /// <summary>
        /// For a modified type, the unmodified type is the type that is being modified.
        /// So `Foo` in `Foo[]`, `Foo*`, or `ref Foo`.
        /// (Not sure if it's possible for this to be null but `IsModifiedType` true)
        /// </summary>
        /// <returns></returns>
        public MrType GetUnmodifiedType()
        {
            if(!IsModifiedType)
            {
                return null;
            }

            var unmodifiedTypeName = GetUnmodifiedTypeName(GetFullName(), out var arrayRank, out bool isReference, out bool isPointer);

            if(arrayRank == null && !isReference && !isPointer)
            {
                Debug.Assert(false, "Modified type isn't a modified type");
                return null;
            }

            if(IsTypeCode)
            {
                return CreatePrimitiveType(this.TypeCode);
            }

            MrType unmodifiedType = null;
            var found = this.Assembly?.LoadContext?.TryFindMrType(unmodifiedTypeName, out unmodifiedType);
            if(found.HasValue && found.Value)
            {
                Debug.Assert(unmodifiedType != null);
                return unmodifiedType;
            }

            if(this.Assembly == null)
            {
                Debug.Assert(false, "Non-primitive type should have an Assembly");
                return null;
            }

            return this.Assembly.LoadContext.CreateFakeType(unmodifiedTypeName);
        }

        public ImmutableArray<MrType> GetFunctionPointerParameterTypes()
        {
            if (!IsFunctionPointer)
            {
                throw new InvalidOperationException("Not a function pointer");
            }

            return _functionPointerSignature.ParameterTypes.ToImmutableArray();
        }


        /// <summary>
        /// If this type is a generic parameter, for example the T in List of T, 
        /// get the attributes and constraints, otherwise return false.
        /// </summary>
        public bool TryGetGenericParameterAttributesAndConstraints(
            out GenericParameterAttributes parameterAttributes,
            out ImmutableArray<MrType> constraints)
        {
            parameterAttributes = GenericParameterAttributes.None;
            constraints = ImmutableArray<MrType>.Empty;

            if (!IsGenericParameter)
            {
                return false;
            }

            // Get the GenericParameter struct from its handle
            var genericParameter = this.Assembly.Reader.GetGenericParameter(this.GenericParameterHandle.Value);

            // Set the [out] value with the attributes (things like Covariant and ReferenceConstraint)
            parameterAttributes = genericParameter.Attributes;

            // Get the constraint handles (if any). Things like String in "where T : String"
            var genericConstraintHandles = genericParameter.GetConstraints();
            var count = genericConstraintHandles.Count;
            if (count == 0)
            {
                // The [out] attributes were already set, and there are no type constraints
                return true;
            }

            // Convert the constraint handles into types 
            var constraintList = new List<MrType>(count);
            foreach (var genericConstraintHandle in genericConstraintHandles)
            {
                var genericConstraint = this.Assembly.Reader.GetGenericParameterConstraint(genericConstraintHandle);
                constraintList.Add(this.Assembly.GetTypeFromEntityHandle(genericConstraint.Type, this.TypeDefinition));
            }
            constraints = constraintList.ToImmutableArray(); // [out] parameter

            return true;
        }

        /// <summary>
        /// Set the arguments that at least partially close an open type. For example String in List of String.
        /// </summary>
        /// <param name="typeArguments"></param>
        internal void SetGenericArguments(IEnumerable<MrType> typeArguments)
        {
            // For a fake type, just remember the arguments
            if (IsFakeType || IsTypeCode)
            {
                _typeArguments = typeArguments.ToImmutableArray();
                return;
            }

            // We already have the type arguments, now get the type parameters
            // There will be as many arguments as parameters. If the type isn't fully closed,
            // some of the typeArguments will actually be parameters (IsGenericParameter will be true)
            var typeParameters = GetGenericTypeParameters();

            // Create a copy of the type arguments and keep them. For each cloned type
            // argument, save its parameter name so that we can use it later (for example if a property
            // on an open generic type is of type T1, we need to know that T1 has been closed as a string).
            int index = 0;
            var argsList = new List<MrType>();
            foreach (var typeArgument in typeArguments)
            {
                // bugbug: When is this valid? This happens for the Windows SDK NuGet
                // (the one generated by cs/winrt)
                if (typeArgument == null)
                {
                    continue;
                }

                var newArg = new MrType(typeArgument);
                newArg.TypeParameterName = typeParameters[index++].GetName();
                argsList.Add(newArg);
            }

            _typeArguments = argsList.ToImmutableArray();
        }

        /// <summary>
        /// If this type is a generic parameter, this will be the type parameter name (e.g. "T" in List of T).
        /// </summary>
        public string TypeParameterName { get; private set; }

        /// <summary>
        /// Return the type arguments of a generic type. Some or all of the returned 'arguments'
        /// may actually still be parameters (this may not be a fully closed type).
        /// </summary>
        /// <returns></returns>
        public ImmutableArray<MrType> GetGenericArguments()
        {
            // If type arguments were set in, return that. That doesn't mean that this is 
            // a closed type though, as the type arguments can be IsGenericParameter. In fact
            // all the arguments might be parameters, in which case this is redundant with
            // calling GetGenericTypeParameters().

            if (_typeArguments != null)
            {
                return _typeArguments.ToImmutableArray<MrType>();
            }

            // No type arguments were provided, but if this type has type parameters,
            // return those, with the IsGenericParameter set.
            if (GetHasGenericParameters())
            {
                return GetGenericTypeParameters();
            }

            return ImmutableArray<MrType>.Empty;
        }

        bool? _hasGenericParameters = null;
        public bool GetHasGenericParameters()
        {
            if (_hasGenericParameters != null)
            {
                return _hasGenericParameters == true;
            }

            if (IsTypeCode)
            {
                _hasGenericParameters = false;
                return false;
            }

            // Not sure how better to detect, but ECMA 335 requires generic types
            // to have a grave accent character.
            var name = GetName();
            _hasGenericParameters = name.LastIndexOf('`') != -1;
            return _hasGenericParameters == true;
        }

        /// <summary>
        /// Get this types base type. Returns null if the base is Object, Enum, or ValueType
        /// </summary>
        /// <returns></returns>
        public MrType GetBaseType()
        {
            if (IsTypeCode || IsFakeType || IsGenericParameter || IsFunctionPointer)
            {
                return null;
            }

            if (TypeDefinition.BaseType.IsNil)
            {
                return null;
            }

            // Sample code:
            // http://source.roslyn.codeplex.com/#Microsoft.CodeAnalysis/MetadataReader/MetadataDecoder.cs,9a074c9da0289503

            return Assembly.GetTypeFromEntityHandle(TypeDefinition.BaseType, TypeDefinition);
        }

        /// <summary>
        /// Get the type parameters of a generic type (e.g. the T in List of T).
        /// </summary>
        /// <returns></returns>
        public ImmutableArray<MrType> GetGenericTypeParameters()
        {
            // For fake types, give inferred type parameters
            if (IsFakeType || IsTypeCode)
            {
                if (_typeArguments != null)
                {
                    return _typeArguments.Select(a => new MrType(a.GetFullName(), Assembly)).ToImmutableArray();
                }
                else
                {
                    return ImmutableArray<MrType>.Empty;
                }
            }

            // Sample:
            // https://github.com/dotnet/corefx/blob/d3911035f2ba3eb5c44310342cc1d654e42aa316/src/System.Reflection.Metadata/tests/Metadata/Decoding/SignatureDecoderTests.cs

            // Wrap generic type parameters as MRTypes

            var genericTypeParameters = TypeDefinition
                .GetGenericParameters()
                .Select(h => new MrType(h, Assembly))
                .ToImmutableArray();

            return genericTypeParameters;
        }

        /// <summary>
        /// Get interfaces implemented by this type
        /// </summary>
        /// <returns></returns>

        public ImmutableArray<MrType> GetInterfaces(bool publicOnly = true)
        {
            if (IsTypeCode || IsFakeType || IsGenericParameter || IsFunctionPointer)
            {
                return ImmutableArray<MrType>.Empty;

            }

            var interfaceImplementationHandles = TypeDefinition.GetInterfaceImplementations();
            return (from h in interfaceImplementationHandles
                    let interfaceImplementation = Assembly.Reader.GetInterfaceImplementation(h)
                    let interfaceType = Assembly.GetTypeFromEntityHandle(interfaceImplementation.Interface, TypeDefinition)
                    select interfaceType)
                    .ToImmutableArray<MrType>();

        }

        public override string ToString()
        {
            return GetPrettyFullName();
        }

        /// <summary>
        /// Get the namespace/name of this type. This will return generic types as Foo`2, use GetPrettyFullName 
        /// to get Foo of T1,T2
        /// </summary>
        /// <returns></returns>
        public string GetFullName()
        {
            return GetFullName(prettyName: false);
        }

        /// <summary>
        /// Get the namespace/name of this type. For generic types this gets a better looking name
        /// then GetFullName
        /// </summary>
        /// <returns></returns>
        public string GetPrettyFullName()
        {
            return GetFullName(prettyName: true);
        }

        private string GetFullName(bool prettyName)
        {
            var ns = GetNamespace();

            if (string.IsNullOrEmpty(ns))
            {
                return GetName(prettyName);
            }
            else
            {
                return $"{ns}.{GetName(prettyName)}";
            }
        }

        public string GetNamespace()
        {
            if (IsFakeType)
            {
                return $"{_fakeNamespace}";
            }

            if (IsTypeCode)
            {
                return "System";
            }

            if (IsGenericParameter)
            {
                return null;
            }

            if (IsFunctionPointer)
            {
                return null;
            }

            return TypeDefinition.Namespace.AsString(Assembly);
        }

        public bool IsPublic
        {
            get
            {
                if (IsTypeCode || IsFakeType || IsFunctionPointer)
                {
                    return true;
                }

                // TypeAttributes doesn't look like a bit mask, but it is, so you can't just check for Public
                var maskedAttributes = TypeDefinition.Attributes & TypeAttributes.VisibilityMask;
                return maskedAttributes == TypeAttributes.Public || maskedAttributes == TypeAttributes.NestedPublic;
            }
        }

        public bool IsAbstract
        {
            get
            {
                if (IsTypeCode || IsFakeType || IsFunctionPointer)
                {
                    return false;
                }

                var attributes = TypeDefinition.Attributes;
                return attributes.HasFlag(TypeAttributes.Abstract);
            }
        }

        public bool IsInternal
        {
            get
            {
                if (IsTypeCode || IsFakeType || IsFunctionPointer)
                {
                    return false;
                }

                var attributes = TypeDefinition.Attributes & TypeAttributes.VisibilityMask;
                return
                    !attributes.HasFlag(TypeAttributes.Public) && !IsNestedType
                    || attributes == TypeAttributes.NestedAssembly // Internal
                    || attributes == TypeAttributes.NestedFamORAssem; // protected internal
            }
        }

        public bool IsPrivate
        {
            get
            {
                if (IsTypeCode || IsFakeType || IsFunctionPointer)
                {
                    return false;
                }

                return
                    (TypeDefinition.Attributes & TypeAttributes.VisibilityMask)
                    == TypeAttributes.NestedPrivate;
            }
        }


        public bool IsProtected
        {
            get
            {
                if (IsTypeCode || IsFakeType || IsFunctionPointer)
                {
                    return false;
                }

                var attributes = TypeDefinition.Attributes & TypeAttributes.VisibilityMask;

                return
                    attributes == TypeAttributes.NestedFamily
                    || attributes == TypeAttributes.NestedFamORAssem;

            }
        }

        public bool IsStatic
        {
            get
            {
                if (IsFakeType)
                {
                    EnsureFakeSystemType();
                    if (_systemType != null)
                    {
                        return _systemType.IsSealed && _systemType.IsAbstract;
                    }
                }

                if (IsTypeCode || IsFunctionPointer)
                {
                    return false;
                }

                // Static is indicated metadata by Abstract+Sealed (which is impossible)
                var attributes = TypeDefinition.Attributes;
                return attributes.HasFlag(TypeAttributes.Abstract) && attributes.HasFlag(TypeAttributes.Sealed);
            }
        }

        Type _systemType = null;
        bool _systemTypeChecked = false;

        void EnsureFakeSystemType()
        {
            if (_systemTypeChecked || !IsFakeType)
            {
                return;
            }
            _systemTypeChecked = true;

            var ns = GetNamespace();
            if (ns != "System" && !ns.StartsWith("System."))
            {
                return;
            }

            var types = typeof(string).Assembly.GetExportedTypes();
            var fullName = this.GetFullName();

            _systemType = types.FirstOrDefault((t) => t.FullName == fullName);

            return;
        }

        public bool IsClass
        {
            get
            {
                if (IsFakeType)
                {
                    EnsureFakeSystemType();
                    if (_systemType != null)
                    {
                        return _systemType.IsClass;
                    }

                    return true; // Random choice
                }

                if (IsTypeCode)
                {
                    // Everything but Object is a value type
                    // 
                    return TypeCode == PrimitiveTypeCode.Object
                        || TypeCode == PrimitiveTypeCode.String;
                }

                if (IsGenericParameter)
                {
                    // Don't know if an open type will be closed as a class or not. 
                    // (Calling TypeDefinition.Attributes will throw)
                    return false;
                }

                return !IsEnum
                       && !IsStruct
                       && !IsFunctionPointer
                       && TypeDefinition.Attributes.HasFlag(TypeAttributes.Class)
                       && !TypeDefinition.Attributes.HasFlag(TypeAttributes.Interface);
            }
        }

        public bool IsInterface
        {
            get
            {
                if (IsFakeType)
                {
                    EnsureFakeSystemType();
                    if (_systemType != null)
                    {
                        return _systemType.IsInterface;
                    }

                    return false; // Randomly assuming class
                }

                if (IsTypeCode || IsFunctionPointer)
                {
                    return false;
                }

                if (IsGenericParameter)
                {
                    // Don't know if an open type will be closed as an interface or not. 
                    // (Calling TypeDefinition.Attributes will throw)
                    return false;
                }

                return TypeDefinition.Attributes.HasFlag(TypeAttributes.Interface);
            }
        }


        public bool IsStruct
        {
            get
            {
                if (IsFakeType)
                {
                    EnsureFakeSystemType();
                    if (_systemType != null)
                    {
                        return _systemType.IsValueType && !_systemType.IsEnum;
                    }

                    return false; // Randomly assuming class
                }

                if (IsTypeCode || IsFunctionPointer)
                {
                    return false;
                }

                if (IsGenericParameter)
                {
                    // Don't know if an open type will be closed as a struct or not. 
                    return false;
                }

                // Structs have System.ValueType as the base

                var baseType = GetBaseType();
                if (baseType == null)
                {
                    return false;
                }

                // Save an allocation by asking for name/namespace separately
                return baseType.GetName() == "ValueType"
                       &&
                       baseType.GetNamespace() == "System";
            }
        }

        public bool IsFamily
        {
            get
            {
                if (this.IsTypeCode || this.IsFakeType || IsFunctionPointer)
                {
                    return false;
                }

                var attributes = this.TypeDefinition.Attributes;
                return attributes.HasFlag(TypeAttributes.NestedFamANDAssem)
                    || attributes.HasFlag(TypeAttributes.NestedFamily)
                    || attributes.HasFlag(TypeAttributes.NestedFamORAssem) && !this.IsInternal;
            }
        }

        public bool IsEnum
        {
            get
            {
                if (IsFakeType || IsTypeCode || IsFunctionPointer)
                {
                    return false;
                }

                // Enums have System.Enum as the base

                var baseType = GetBaseType();
                if (baseType == null)
                {
                    return false;
                }

                // Save an allocation by asking for name/namespace separately
                return baseType.GetName() == "Enum"
                       &&
                       baseType.GetNamespace() == "System";
            }
        }

        public bool IsSealed
        {
            get
            {
                if (IsFakeType || IsFunctionPointer)
                {
                    return true;
                }
                else if (IsTypeCode)
                {
                    return TypeCode != PrimitiveTypeCode.Object;
                }

                return this.TypeDefinition.Attributes.HasFlag(TypeAttributes.Sealed);
            }
        }

        public string AssemblyLocation
        {
            get
            {
                if (this.IsFakeType || this.IsTypeCode || IsFunctionPointer)
                {
                    return string.Empty;
                }
                else
                {
                    return this.Assembly.Location;
                }
            }
        }

        /// <summary>
        /// Get the underlying primitive type of an enum, used to determine its size. Null if not an enum.
        /// </summary>
        /// <returns></returns>
        public MrType GetUnderlyingEnumType()
        {
            if (!IsEnum)
            {
                return null;
            }

            /* II.14.3 Enums
             * The symbols of an enum are represented by an underlying integer type: one of 
             * { bool, char, int8, unsigned int8, int16, unsigned int16, int32, unsigned int32, int64, unsigned int64, native int, unsigned native int }
             */


            if (this.IsFakeType)
            {
                // This type isn't in any of the loaded assemblies, we're just dummy-ing it out with the name.
                // So we can't get at the underlying type. Int32 or UInt32 is probably a good guess, but that could lead
                // to weird downstream problems in blob readers. We can't change this method signature to return some special
                // value (this is called by System.Metadata.Reflection), so instead throw, and let the actual
                // caller (in MiddleweightReflection) catch it.

                throw new MrException("Can't get underlying enum type for unknown type");
            }

            // Enums have a private special-name field named "value__" to store their value.
            // This is the enum's underlying type

            var fields = this.GetFields(publicishOnly: false);
            foreach (var field in fields)
            {
                if (field.GetName() == "value__")
                {
                    return field.GetFieldType();
                }
            }

            throw new Exception("Can't find underlying enum type");
        }

        /// <summary>
        /// The raw name of the type. Use GetPrettyName to make generic types look better.
        /// </summary>
        /// <returns></returns>
        override public string GetName()
        {
            return GetName(prettyName: false);
        }

        /// <summary>
        /// The name of the type, with generic types in List of T form rather than List`1
        /// </summary>
        /// <returns></returns>
        public string GetPrettyName()
        {
            return GetName(prettyName: true);
        }

        string GetName(bool prettyName)
        {
            // String will typically require less allocations than StringBuilder
            string name;

            if (IsFakeType)
            {
                name = $"{_fakeName}";
            }
            else if (IsTypeCode)
            {
                name = TypeCodeToName(TypeCode);
            }
            else if (IsGenericParameter)
            {
                name = $"{Assembly.Reader.GetGenericParameter(GenericParameterHandle.Value).Name.AsString(Assembly)}";
            }
            else if (IsFunctionPointer)
            {
                var result = new StringBuilder();
                result.Append($"unsafe delegate* ");

                if (_functionPointerSignature.Header.CallingConvention == SignatureCallingConvention.Default)
                {
                    result.Append("managed");
                }
                else
                {
                    result.Append($"unmanaged[{_functionPointerSignature.Header.CallingConvention.ToString()}]");
                }

                result.Append($"<");
                result.Append(this.GetFunctionPointerReturnType().GetPrettyName());

                foreach (var parameterType in GetFunctionPointerParameterTypes())
                {
                    result.Append(",");
                    result.Append(parameterType.GetPrettyName());
                }
                result.Append(">");
                name = result.ToString();
            }

            else if (Assembly.Reader != null)
            {
                name = GetTypeNameFromTypeDefinition(Assembly.Reader, TypeDefinition);
            }

            else
            {
                throw new Exception("Internal error, can't find type name");
            }

            if (prettyName)
            {
                // Clean up generic types. E.g. List<T> rather than List`1
                var typeArguments = GetGenericArguments();
                if (!typeArguments.IsEmpty)
                {
                    var lastIndex = name.LastIndexOf('`');
                    name = name.Substring(0, lastIndex);
                    name = name + $"<{string.Join(",", typeArguments)}>";
                }
            }

                if (IsArray)
                {
                    name = name + "[]";
                }

                if (IsReference)
                {
                    name = name + "&";
                }

                if (IsPointer)
                {
                    name = name + "*";
                }

            return name;
        }

        /// <summary>
        /// Get the type name without the array ([]), reference (&), or pointer (*) suffixes.
        /// </summary>
        internal static string GetUnmodifiedTypeName(
            string name,
            out int? arrayRank, // bugbug: why isn't this just an int?
            out bool isReference,
            out bool isPointer)
        {

            arrayRank = null;
            if (name.Contains("["))
            {
                // [] is a rank of 1, [,] is a rank of 2, etc.
                arrayRank = name.Split(',').Length;
            }

            isReference = name.Contains("&");
            isPointer = name.Contains("*");

            name = name.Split('[')[0];
            name = name.Split('*')[0];
            name = name.Split('&')[0];

            return name;
        }

        /// <summary>
        /// The methods and constructors for this type. This is one method rather than two to be more efficient 
        /// (fewer allocations).
        /// </summary>
        /// <param name="publicishOnly">If true, only return public or protected methods (for an unsealed type)</param>
        public void GetMethodsAndConstructors(
            out ImmutableArray<MrMethod> methods,
            out ImmutableArray<MrMethod> constructors,
            bool publicishOnly = true)
        {
            if (IsFakeType || IsTypeCode || IsFunctionPointer)
            {
                methods = constructors = ImmutableArray<MrMethod>.Empty;
                return;
            }

            var methodDefinitionHandles = TypeDefinition.GetMethods();
            List<MrMethod> mrMethods = null;
            List<MrMethod> mrConstructors = null;

            foreach (var methodDefinitionHandle in methodDefinitionHandles)
            {
                var methodDefinition = Assembly.Reader.GetMethodDefinition(methodDefinitionHandle);

                var mrMethod = MrMethod.TryGetMethod(methodDefinitionHandle, this, publicishOnly);
                if (mrMethod == null)
                {
                    continue;
                }

                var isConstructor = mrMethod.GetIsConstructor();

                // Ignore things like the get_/set_ methods for properties.
                // Don't ignore constructors though; SpecialName is set for the static constructor
                if (mrMethod.MethodDefinition.Attributes.HasFlag(MethodAttributes.SpecialName) && !isConstructor)
                {
                    continue;
                }

                if (isConstructor)
                {
                    if (mrConstructors == null)
                    {
                        mrConstructors = new List<MrMethod>(methodDefinitionHandles.Count);
                    }
                    mrConstructors.Add(mrMethod);
                }
                else
                {
                    if (mrMethods == null)
                    {
                        mrMethods = new List<MrMethod>(methodDefinitionHandles.Count);
                    }
                    mrMethods.Add(mrMethod);
                }
            }

            methods = mrMethods == null ? ImmutableArray<MrMethod>.Empty : mrMethods.ToImmutableArray();
            constructors = mrConstructors == null ? ImmutableArray<MrMethod>.Empty : mrConstructors.ToImmutableArray();
        }

        /// <summary>
        /// Get properties for this type
        /// </summary>
        /// <param name="publicishOnly">If true, only return public or protected methods (for an unsealed type)</param>
        public ImmutableArray<MrProperty> GetProperties(bool publicishOnly = true)
        {
            if (IsFakeType || IsTypeCode || IsFunctionPointer)
            {
                return ImmutableArray<MrProperty>.Empty;
            }

            var propertyDefinitionHandles = TypeDefinition.GetProperties();
            List<MrProperty> propertiesList = null;

            foreach (var propertyDefinitionHandle in propertyDefinitionHandles)
            {
                var property = MrProperty.TryGetProperty(this, propertyDefinitionHandle, publicishOnly);
                if (property != null)
                {
                    if (propertiesList == null)
                    {
                        propertiesList = new List<MrProperty>(propertyDefinitionHandles.Count);
                    }
                    propertiesList.Add(property);
                }
            }

            return propertiesList == null ? ImmutableArray<MrProperty>.Empty : propertiesList.ToImmutableArray();
        }

        public ImmutableArray<MrType> GetNestedTypes()
        {
            List<MrType> mrTypeList = null;
            var nestedTypeDefinitionHandles = this.TypeDefinition.GetNestedTypes();
            foreach (var nestedTypeDefinition in nestedTypeDefinitionHandles)
            {
                if (mrTypeList == null)
                {
                    mrTypeList = new List<MrType>();
                }
                mrTypeList.Add(MrType.CreateFromTypeDefinition(nestedTypeDefinition, this.Assembly));
            }

            return mrTypeList == null ? ImmutableArray<MrType>.Empty : mrTypeList.ToImmutableArray();
        }

        public bool IsNestedType => this.TypeDefinition.IsNested;

        /// <summary>
        /// Get events tfor this type
        /// </summary>
        /// <param name="publicishOnly">If true, only return public or protected methods (for an unsealed type)</param>
        public ImmutableArray<MrEvent> GetEvents(bool publicishOnly = true)
        {
            if (IsFakeType || IsTypeCode || IsFunctionPointer)
            {
                return ImmutableArray<MrEvent>.Empty;
            }

            var eventDefinitionHandles = TypeDefinition.GetEvents();
            List<MrEvent> eventsList = null;

            foreach (var eventDefinitionHandle in eventDefinitionHandles)
            {
                var ev = MrEvent.TryGetEvent(eventDefinitionHandle, this, publicishOnly);
                if (ev != null && ev.IsValid)
                {
                    if (eventsList == null)
                    {
                        eventsList = new List<MrEvent>(eventDefinitionHandles.Count);
                    }
                    eventsList.Add(ev);
                }
            }

            return eventsList == null ? ImmutableArray<MrEvent>.Empty : eventsList.ToImmutableArray();
        }

        /// <summary>
        /// Get fields for this type
        /// </summary>
        /// <param name="publicishOnly">If true, only return public or protected methods (for an unsealed type)</param>
        public ImmutableArray<MrField> GetFields(bool publicishOnly = true)
        {
            if (IsFakeType || IsTypeCode || IsFunctionPointer)
            {
                return ImmutableArray<MrField>.Empty;
            }

            var fieldDefinitionHandles = TypeDefinition.GetFields();
            List<MrField> fieldsList = null;

            foreach (var fieldDefinitionHandle in fieldDefinitionHandles)
            {
                var field = MrField.TryCreate(fieldDefinitionHandle, this, publicishOnly);
                if (field == null)
                {
                    continue;
                }

                // For a property named Foo, the compiler may generate a private field named
                // <Foo>k__BackingField
                if (field.IsPrivate && field.GetName().EndsWith(">k__BackingField"))
                {
                    continue;
                }

                // Bugbug: For an event named FooHappened, the compiler generates a private field named
                // FooHappened. How to filter this out? If you're only looking for public types and the
                // event is public, it's not an issue. But if you're also looking non-public types,
                // you'll see FooHappened as both an event and a field. There doesn't seem to be any
                // kind of marking in the metadata to indicate that the field is special.
                // I think maybe you need to check to see if there's an event and field by the same
                // name, but I hate to take that overhead on every field lookup.

                if (fieldsList == null)
                {
                    fieldsList = new List<MrField>(fieldDefinitionHandles.Count);
                }
                fieldsList.Add(field);
            }

            return fieldsList == null ? ImmutableArray<MrField>.Empty : fieldsList.ToImmutableArray();
        }


        static private string GetTypeNameFromTypeDefinition(MetadataReader reader, TypeDefinition typeDefinition)
        {
            var typeNameBuilder = new StringBuilder();
            if (typeDefinition.IsNested)
            {
                // This is a nested type, like B nested in A, so we want to produce the name
                // "A+B". GetDeclaryingType() will return A, so we'll start "A+", and then below
                // (after the 'if' block) we'll add the "B". Note that this is recursing because we 
                // could be a nested type inside a nested type.

                var declaringTypeDefinitionHandle = typeDefinition.GetDeclaringType();
                var declaringTypeDefinition = reader.GetTypeDefinition(declaringTypeDefinitionHandle);
                typeNameBuilder.Append(GetTypeNameFromTypeDefinition(reader, declaringTypeDefinition));
                typeNameBuilder.Append("+");
            }
            typeNameBuilder.Append(reader.GetString(typeDefinition.Name));

            return typeNameBuilder.ToString();
        }

        string TypeCodeToName(PrimitiveTypeCode typeCode)
        {
            switch (typeCode)
            {
                case PrimitiveTypeCode.Boolean:
                    return "Boolean";

                case PrimitiveTypeCode.Byte:
                    return "Byte";

                case PrimitiveTypeCode.Char:
                    return "Char";

                case PrimitiveTypeCode.Double:
                    return "Double";

                case PrimitiveTypeCode.Int16:
                    return "Int16";

                case PrimitiveTypeCode.Int32:
                    return "Int32";

                case PrimitiveTypeCode.Int64:
                    return "Int64";

                case PrimitiveTypeCode.IntPtr:
                    return "Native Nnt";

                case PrimitiveTypeCode.Object:
                    return "Object";

                case PrimitiveTypeCode.SByte:
                    return "Int8";

                case PrimitiveTypeCode.Single:
                    return "Single";

                case PrimitiveTypeCode.String:
                    return "String";

                case PrimitiveTypeCode.TypedReference:
                    return "typedref";

                case PrimitiveTypeCode.UInt16:
                    return "UInt16";

                case PrimitiveTypeCode.UInt32:
                    return "UInt32";

                case PrimitiveTypeCode.UInt64:
                    return "UInt64";

                case PrimitiveTypeCode.UIntPtr:
                    return "Native UInt";

                case PrimitiveTypeCode.Void:
                    return "Void";

                default:
                    Debug.Assert(false);
                    throw new ArgumentOutOfRangeException(nameof(typeCode));
            }
        }

        override public ImmutableArray<MrCustomAttribute> GetCustomAttributes()
        {
            if (IsTypeCode || IsFakeType || IsGenericParameter || IsFunctionPointer)
            {
                return ImmutableArray<MrCustomAttribute>.Empty;
            }

            var customAttributeHandles = this.TypeDefinition.GetCustomAttributes();
            var customAttributes = new List<MrCustomAttribute>(customAttributeHandles.Count);
            foreach (var customAttributeHandle in customAttributeHandles)
            {
                var customAttribute = new MrCustomAttribute(customAttributeHandle, this, Assembly);
                customAttributes.Add(customAttribute);
            }

            return customAttributes.ToImmutableArray();
        }

        /// <summary>
        /// Get a method named "Invoke". This is intended to be called on delegate types.
        /// </summary>
        /// <returns></returns>
        public MrMethod GetInvokeMethod()
        {
            if (IsFakeType || IsFunctionPointer)
            {
                return null;
            }

            // Don't use this.GetMethods, because it filters out SpecialType methods like Delegate.Invoke
            foreach (var methodDefinitionHandle in this.TypeDefinition.GetMethods())
            {
                var methodDefinition = this.Assembly.Reader.GetMethodDefinition(methodDefinitionHandle);
                var name = methodDefinition.Name.AsString(this.Assembly);
                if (name == "Invoke")
                {
                    return MrMethod.TryGetMethod(methodDefinitionHandle, this, publicishOnly: false);
                }
            }

            Debug.Assert(this.GetFullName() == "System.MulticastDelegate"
                          || this.GetFullName() == "System.Delegate");
            return null;
        }
    }
}
