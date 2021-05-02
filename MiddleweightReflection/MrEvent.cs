using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace MiddleweightReflection
{
    public class MrEvent : MrTypeAndMemberBase
    {
        public EventDefinitionHandle DefinitionHandle { get; }
        override public MrType DeclaringType { get; }
        public EventDefinition Definition { get; }

        private MrEvent(
            EventDefinitionHandle eventDefinitionHandle, 
            EventDefinition eventDefinition,
            MrType declaringType)
        {
            DefinitionHandle = eventDefinitionHandle;
            Definition = eventDefinition;
            DeclaringType = declaringType;
        }

        public override bool Equals(object obj)
        {
            var other = obj as MrEvent;
            var prolog = MrLoadContext.OverrideEqualsProlog(this, other);
            if (prolog != null)
            {
                return (bool)prolog;
            }

            if (this.DeclaringType != other.DeclaringType
                || this.DefinitionHandle != other.DefinitionHandle)
            {
                return false;
            }

            return true;
        }

        public static bool operator ==(MrEvent operand1, MrEvent operand2)
        {
            return MrLoadContext.OperatorEquals(operand1, operand2);
        }

        public static bool operator !=(MrEvent operand1, MrEvent operand2)
        {
            return !(operand1 == operand2);
        }

        public override int GetHashCode()
        {
            return this.DefinitionHandle.GetHashCode();
        }


        static internal MrEvent TryGetEvent(
            EventDefinitionHandle eventDefinitionHandle, 
            MrType declaringType,
            bool publicishOnly )
        {
            var eventDefinition = declaringType.Assembly.Reader.GetEventDefinition(eventDefinitionHandle);

            if (publicishOnly)
            {
                var eventAccessors = eventDefinition.GetAccessors();
                var adderDefinition = declaringType.Assembly.Reader.GetMethodDefinition(eventAccessors.Adder);
                if (!MrMethod.AreAttributesPublicish(adderDefinition.Attributes, declaringType))
                {
                    return null;
                }
            }

            return new MrEvent(eventDefinitionHandle, eventDefinition, declaringType);
        }


        public void GetAccessors(out MrMethod adder, out MrMethod remover, bool publicishOnly = true)
        {
            var eventAccessors = Definition.GetAccessors();

            adder = MrMethod.TryGetMethod(eventAccessors.Adder, DeclaringType, publicishOnly);
            remover = MrMethod.TryGetMethod(eventAccessors.Remover, DeclaringType, publicishOnly);
        }


        public MrMethod GetInvoker()
        {
            return this.GetEventType().GetInvokeMethod();
        }

        public override string ToString()
        {
            return $"{DeclaringType.GetName()}.{GetName()}";
        }

        public ParsedMethodAttributes GetMemberModifiers(bool publichishOnly = true)
        {
            GetAccessors(out var adder, out var remover, publichishOnly);
            return adder.GetParsedMethodAttributes();
        }

        public bool IsValid
        {
            get
            {
                var eventAccessors = Definition.GetAccessors();
                if (eventAccessors.Adder.IsNil)
                {
                    return false;
                }

                return true;
            }
        }

        override public ImmutableArray<MrCustomAttribute> GetCustomAttributes()
        {
            var customAttributeHandles = this.Definition.GetCustomAttributes();
            var customAttributes = MrAssembly.GetCustomAttributesFromHandles(customAttributeHandles, this.DeclaringType);
            return customAttributes.ToImmutableArray();
        }

        public MrType GetEventType()
        {
            var eventTypeEntityHandle = Definition.Type;
            var eventType = DeclaringType.Assembly.GetTypeFromEntityHandle(eventTypeEntityHandle, DeclaringType.TypeDefinition);
            return eventType;
        }

        override public string GetName()
        {
            return Definition.Name.AsString(DeclaringType.Assembly);
        }

    }
}
