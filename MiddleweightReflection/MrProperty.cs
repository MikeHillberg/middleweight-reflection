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
    /// A property of an MRType
    /// </summary>
    public class MrProperty : IMrHasCustomAttributes, IMrTypeMember
    {
        public MrType DeclaringType { get; }
        public PropertyDefinitionHandle DefinitionHandle { get; }
        public PropertyDefinition Definition { get; }

        public MrMethod Getter { get; private set; }
        public MrMethod Setter { get; private set; }

        private MrProperty(
            MrType declaringType, 
            PropertyDefinitionHandle propertyDefinitionHandle,
            PropertyDefinition propertyDefinition)
        {
            DeclaringType = declaringType;
            DefinitionHandle = propertyDefinitionHandle;
            Definition = propertyDefinition;
        }

        internal static MrProperty TryGetProperty(
            MrType declaringType, 
            PropertyDefinitionHandle propertyDefinitionHandle,
            bool publicishOnly)
        {
            var propertyDefinition = declaringType.Assembly.Reader.GetPropertyDefinition(propertyDefinitionHandle);

            var propertyAccessors = propertyDefinition.GetAccessors();
            var mrGetter = TryGetEtter(
                declaringType,
                propertyAccessors.Getter,
                publicishOnly);

            var mrSetter = TryGetEtter(
                declaringType,
                propertyAccessors.Setter,
                publicishOnly);

            if(mrGetter != null || mrSetter != null)
            {
                return new MrProperty(declaringType, propertyDefinitionHandle, propertyDefinition)
                {
                    Getter = mrGetter,
                    Setter = mrSetter
                };
            }

            return null;
        }

        private static MrMethod TryGetEtter(
            MrType declaringType, 
            MethodDefinitionHandle methodDefinitionHandle,
            bool publicishOnly)
        {
            if (!methodDefinitionHandle.IsNil)
            {
                var methodDefinition = declaringType.Assembly.Reader.GetMethodDefinition(methodDefinitionHandle);
                var attributes = methodDefinition.Attributes;
                if (!publicishOnly || MrMethod.AreAttributesPublicish(attributes, declaringType))
                {
                    return new MrMethod(methodDefinitionHandle, declaringType, methodDefinition );
                }
            }
            return null;
        }

        public override string ToString()
        {
            return $"MrProperty: {DeclaringType.GetName()}.{GetName()}";
        }

        /// <summary>
        /// The property's getter and setter, either of which could be null
        /// </summary>
        static void GetGetterAndSetter(
            PropertyDefinition propertyDefinition, 
            MrType declaringType,
            bool publicishOnly,
            out MrMethod getter, out MrMethod setter)
        {
            var propertyAccessors = propertyDefinition.GetAccessors();

            getter = MrMethod.TryGetMethod(propertyAccessors.Getter, declaringType, publicishOnly);
            setter = MrMethod.TryGetMethod(propertyAccessors.Setter, declaringType, publicishOnly);
        }

        public ImmutableArray<MrCustomAttribute> GetCustomAttributes()
        {
            var customAttributeHandles = this.Definition.GetCustomAttributes();
            var customAttributes = MrAssembly.GetCustomAttributesFromHandles(customAttributeHandles, this.DeclaringType);
            return customAttributes.ToImmutableArray();
        }

        /// <summary>
        /// For an indexed property, the type of the property. Null if not indexed.
        /// </summary>
        /// <returns></returns>
        public MrType GetItemType(bool publicishOnly)
        {
            // This could be a getter or a setter method
            MrMethod etterMethod;

            var getterHandle = Definition.GetAccessors().Getter;
            etterMethod = MrMethod.TryGetMethod(getterHandle, this.DeclaringType, publicishOnly);
            if(etterMethod == null)
            {
                var setterHandle = Definition.GetAccessors().Getter;
                etterMethod = MrMethod.TryGetMethod(setterHandle, this.DeclaringType, publicishOnly);
                if (etterMethod == null)
                {
                    // Not an indexed property
                    return null;
                }
            }

            var parameters = etterMethod.GetParameters();
            if (parameters.Length == 0)
            {
                // Not an indexed property
                return null;
            }

            return parameters[0].GetParameterType();
        }

        /// <summary>
        /// Get the property's method attributes, as properties rather than non-trivial bit math
        /// </summary>
        /// <returns></returns>
        public ParsedMethodAttributes GetParsedMethodAttributes()
        {
            //GetGetterAndSetter(out var getter, out var setter);
            if (Getter != null)
                return Getter.GetParsedMethodAttributes();
            else
                return Setter.GetParsedMethodAttributes();
        }


        public PropertyAttributes Attributes => Definition.Attributes;

        public MrType GetPropertyType()
        {
            var context = new MRGenericContext(DeclaringType.GetGenericTypeParameters(), ImmutableArray<MrType>.Empty);
            var propertySignature = Definition.DecodeSignature<MrType, MRGenericContext>(DeclaringType.Assembly.TypeProvider, context);
            return propertySignature.ReturnType;
        }

        public string GetName()
        {
            return Definition.Name.AsString(DeclaringType.Assembly);
        }

    }
}
