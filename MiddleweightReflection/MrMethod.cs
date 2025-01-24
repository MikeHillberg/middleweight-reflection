using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace MiddleweightReflection
{
    /// <summary>
    /// Represents a method on the DeclaringType
    /// </summary>
    public class MrMethod : MrTypeAndMemberBase
    {

        MrType _declaringType;
        override public MrType DeclaringType => _declaringType;
        private void SetDeclaringType(MrType type)
        {
            _declaringType = type;
        }

        public MethodDefinitionHandle MethodDefinitionHandle { get; private set; }
        public MethodDefinition MethodDefinition { get; private set; }
        public MethodSignature<MrType> MethodSignature { get; private set; }

        public override string ToString()
        {
            return $"MrMethod: {DeclaringType.GetPrettyName()}.{GetName()}";
        }




        static internal bool IsPublicMethodAttributes(MethodAttributes attributes)
        {
            return (attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public;
        }

        static internal bool IsProtectedMethodAttributes(MethodAttributes attributes)
        {
            return (attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Family;
        }

        static internal bool IsPrivateMethodAttributes(MethodAttributes attributes)
        {
            return (attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Private;
        }

        static internal bool IsInternalMethodAttributes(MethodAttributes attributes)
        {
            return (attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Assembly;
        }

        internal static bool IsVirtualMethodAttributes(MethodAttributes attributes)
        {
            return attributes.HasFlag(MethodAttributes.Virtual);
        }

        internal static bool IsOverrideMethodAttributes(MethodAttributes attributes)
        {
            return
                attributes.HasFlag(MethodAttributes.Virtual)
                && !attributes.HasFlag(MethodAttributes.NewSlot);
        }

        internal static bool IsSealedMethodAttributes(MethodAttributes attributes)
        {
            return attributes.HasFlag(MethodAttributes.Final);
        }

        internal static bool IsStaticMethodAttributes(MethodAttributes attributes)
        {
            return attributes.HasFlag(MethodAttributes.Static);
        }

        internal static bool IsAbstractMethodAttributes(MethodAttributes attributes)
        {
            return attributes.HasFlag(MethodAttributes.Abstract);
        }

        public ParsedMethodAttributes GetParsedMethodAttributes()
        {
            var attributes = this.MethodDefinition.Attributes;

            var modifiers = new ParsedMethodAttributes();

            modifiers.IsPublic = MrMethod.IsPublicMethodAttributes(attributes);
            modifiers.IsPrivate = MrMethod.IsPrivateMethodAttributes(attributes);
            modifiers.IsInternal = MrMethod.IsInternalMethodAttributes(attributes);
            modifiers.IsProtected = MrMethod.IsProtectedMethodAttributes(attributes);
            modifiers.IsVirtual = MrMethod.IsVirtualMethodAttributes(attributes);
            modifiers.IsOverride = MrMethod.IsOverrideMethodAttributes(attributes);
            modifiers.IsSealed = MrMethod.IsSealedMethodAttributes(attributes);
            modifiers.IsStatic = MrMethod.IsStaticMethodAttributes(attributes);
            modifiers.IsAbstract = MrMethod.IsAbstractMethodAttributes(attributes);

            return modifiers;
        }

        /// <summary>
        /// Same as GetParsedMethodAttributes but a more discoverable name
        /// </summary>
        /// <returns></returns>
        public ParsedMethodAttributes GetMethodAccess()
        {
            return this.GetParsedMethodAttributes();
        }

        private MrMethod(
            MethodDefinitionHandle methodDefinitionHandle,
            MrType declaringType)
        {
            var methodDefinition = DeclaringType.Assembly.Reader.GetMethodDefinition(methodDefinitionHandle);
            Initialize(methodDefinitionHandle, declaringType, methodDefinition);
        }

        static internal MrMethod TryGetMethod(
            MethodDefinitionHandle methodDefinitionHandle,
            MrType declaringType,
            bool publicishOnly)
        {
            if (methodDefinitionHandle.IsNil)
            {
                return null;
            }

            var methodDefinition = declaringType.Assembly.Reader.GetMethodDefinition(methodDefinitionHandle);
            return TryCreateMethod(methodDefinitionHandle, methodDefinition, declaringType, publicishOnly);
        }

        static internal MrMethod TryCreateMethod(
            MethodDefinitionHandle methodDefinitionHandle,
            MethodDefinition methodDefinition,
            MrType declaringType,
            bool publicishOnly)
        {
            var attributes = methodDefinition.Attributes;
            if (!publicishOnly || AreAttributesPublicish(attributes, declaringType))
            {
                // Creating an MrMethod can fail in un-predictable ways inside System.Reflection.Metadata
                // (If a dependent assembly is passed in the LoadContext that's incompatible)
                // Since this is a Try method, catch the exception
                try
                {
                    return new MrMethod(methodDefinitionHandle, declaringType, methodDefinition);
                }
                catch(Exception)
                {
                    // logging?
                }
            }

            return null;
        }


        public MrMethod(
            MethodDefinitionHandle methodDefinitionHandle,
            MrType declaringType,
            MethodDefinition methodDefinition)
        {
            Initialize(methodDefinitionHandle, declaringType, methodDefinition);
        }

        private void Initialize(
            MethodDefinitionHandle methodDefinitionHandle,
            MrType declaringType,
            MethodDefinition methodDefinition)
        {
            SetDeclaringType(declaringType);
            MethodDefinitionHandle = methodDefinitionHandle;

            MethodDefinition = methodDefinition;

            // Decode the method signature

            var genericMethodParameters
                = MethodDefinition
                    .GetGenericParameters()
                    .Select(h => MrType.CreateFromGenericParameterHandle(h, DeclaringType.Assembly))
                    .ToImmutableArray();

            var context = new MRGenericContext(DeclaringType.GetGenericTypeParameters(), genericMethodParameters);

            MethodSignature = MethodDefinition.DecodeSignature<MrType, MRGenericContext>(
                DeclaringType.Assembly.TypeProvider,
                context);
        }

        public override bool Equals(object obj)
        {
            var other = obj as MrMethod;
            var prolog = MrLoadContext.OverrideEqualsProlog(this, other);
            if (prolog != null)
            {
                return (bool)prolog;
            }

            if( this.DeclaringType != other.DeclaringType
                || this.MethodDefinitionHandle != other.MethodDefinitionHandle )
            {
                return false;
            }

            return true;
        }

        public static bool operator ==(MrMethod operand1, MrMethod operand2)
        {
            return MrLoadContext.OperatorEquals(operand1, operand2);
        }

        public static bool operator !=(MrMethod operand1, MrMethod operand2)
        {
            return !(operand1 == operand2);
        }

        public override int GetHashCode()
        {
            return this.MethodDefinitionHandle.GetHashCode();
        }


        public bool GetIsConstructor()
        {
            return MethodDefinition.Name.AsString(DeclaringType.Assembly) == ".ctor";
        }

        override public string GetName()
        {
            return MethodDefinition.Name.AsString(DeclaringType.Assembly);
        }

        public MrType ReturnType
        {
            get
            {
                return MethodSignature.ReturnType;
            }
        }

        /// <summary>
        /// The method's parameters, an emtpy array if none
        public ImmutableArray<MrParameter> GetParameters()
        {
            int typeIndex = 0;
            int handleIndex = 0;

            // MethodDefinition gives parameter handles including the return type. MethodSignature
            // gives parameter types, but not including the return type. We only want the parameters,
            // not the return type, so if this is a non-void method skip the first handle.
            var parameterHandles = MethodDefinition.GetParameters().ToArray();
            if (parameterHandles.Length > MethodSignature.ParameterTypes.Length)
            {
                handleIndex++;
            }

            // Build a List of MRParameters
            var parametersList = new List<MrParameter>(parameterHandles.Length);
            while (handleIndex < parameterHandles.Length)
            {
                parametersList.Add(new MrParameter(this, parameterHandles[handleIndex++], typeIndex++));
            }

            // Return as an array
            return parametersList.ToImmutableArray();
        }

        /// <summary>
        /// This method's custom attributes, empty if none
        /// </summary>
        override public ImmutableArray<MrCustomAttribute> GetCustomAttributes()
        {
            var customAttributeHandles = this.MethodDefinition.GetCustomAttributes();
            var customAttributes = MrAssembly.GetCustomAttributesFromHandles(customAttributeHandles, this.DeclaringType);
            return customAttributes.ToImmutableArray();
        }

        internal static bool AreAttributesPublicish(MethodAttributes attributes, MrType declaringType)
        {
            if (MrMethod.IsPublicMethodAttributes(attributes)
                || MrMethod.IsProtectedMethodAttributes(attributes)
                    && !declaringType.IsSealed)
            {
                return true;
            }

            return false;
        }
    }
}
