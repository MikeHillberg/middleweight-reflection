using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace MiddleweightReflection
{
    public class MrCustomAttribute
    {
        public CustomAttributeHandle Handle { get; }
        public CustomAttribute CustomAttribute { get; }
        public MrAssembly Assembly { get; }
        public MrType TargetType { get; }

        public MrCustomAttribute(CustomAttributeHandle handle, MrType targetType, MrAssembly assembly)
        {
            Handle = handle;
            Assembly = assembly;
            TargetType = targetType;

            CustomAttribute = assembly.Reader.GetCustomAttribute(handle);
        }

        public void GetNameAndNamespace(out string name, out string ns)
        {
            switch (CustomAttribute.Constructor.Kind)
            {
                case HandleKind.MethodDefinition:
                    {
                        var constructorMethodDefinitionHandle = (MethodDefinitionHandle)CustomAttribute.Constructor;
                        var constructorDefinition = Assembly.Reader.GetMethodDefinition(constructorMethodDefinitionHandle);
                        var attributeTypeHandle = constructorDefinition.GetDeclaringType();
                        var attributeTypeDefinition = Assembly.Reader.GetTypeDefinition(attributeTypeHandle);

                        name = attributeTypeDefinition.Name.AsString(Assembly);
                        ns = attributeTypeDefinition.Namespace.AsString(Assembly);
                    }
                    break;

                case HandleKind.MemberReference:
                    {
                        var targetTypeDefinition = Assembly.Reader.GetTypeDefinition(TargetType.TypeDefinitionHandle);
                        var constructorReference = Assembly.Reader.GetMemberReference((MemberReferenceHandle)CustomAttribute.Constructor);
                        var declaringType = Assembly.GetTypeFromEntityHandle(constructorReference.Parent, targetTypeDefinition);

                        name = declaringType.GetName();
                        ns = declaringType.GetNamespace();
                    }
                    break;

                default:
                    throw new Exception("Not supported");
            }
        }

        public override string ToString()
        {
            GetNameAndNamespace(out var name, out var ns);
            return $"{ns}.{name}";
        }
               

        public void GetArguments(
            out ImmutableArray<(MrType Type, object Value)> fixedArguments,
            out ImmutableArray<(string Name, MrType Type, object Value)> namedArguments)
        {
            fixedArguments = ImmutableArray<(MrType, object)>.Empty;
            namedArguments = ImmutableArray<(string, MrType, object)>.Empty;

            var customAttributeTypeProvider = new CustomAttributeTypeProvider(Assembly);

            CustomAttributeValue<MrType> attributeValue;
            try
            {
                attributeValue = CustomAttribute.DecodeValue(customAttributeTypeProvider);
            }
            catch(MrException)
            {
                // We can't decode the value (because it's referencing fake types). Just ignore the arguments.
                return;
            }

            // bugbug: ThreadingAttribute comes out of here as a unit, .Net reflection has it as an int
            // (See TypeViewModel.ThreadingModelValue)

            List<(MrType, object)> fixedArgumentsList = null;
            foreach (var fixedArgument in attributeValue.FixedArguments)
            {
                if(fixedArgumentsList == null)
                {
                    fixedArgumentsList = new List<(MrType, object)>(attributeValue.FixedArguments.Length);
                }

                fixedArgumentsList.Add((fixedArgument.Type, fixedArgument.Value));
            }
            if(fixedArgumentsList != null)
            {
                fixedArguments = fixedArgumentsList.ToImmutableArray();
            }

            List<(string, MrType, object)> namedArgumentsList = null;
            foreach (var namedArgument in attributeValue.NamedArguments)
            {
                if(namedArgumentsList == null)
                {
                    namedArgumentsList = new List<(string, MrType, object)>(attributeValue.NamedArguments.Length);
                }
                namedArgumentsList.Add((namedArgument.Name, namedArgument.Type, namedArgument.Value));
            }

            if (namedArgumentsList != null)
            {
                namedArguments = namedArgumentsList.ToImmutableArray();
            }
        }
    }
}
