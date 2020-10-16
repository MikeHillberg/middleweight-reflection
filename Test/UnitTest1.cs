using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiddleweightReflection;

namespace MRUnitTests
{
    [TestClass]
    public class UnitTest1
    {
        [ClassInitialize]
        static public void Initialize(TestContext context)
        {
            //_testAssembly = Properties.Resources.TestAssembly;
        }

        Assembly _testAssembly;

        [TestMethod]
        public void TestMethod1()
        {
            var loadContext = new MrLoadContext();
            loadContext.AssemblyPathFromName = AssemblyPathFromName;

            _testAssembly = typeof(UnitTestSampleAssembly.Class1<,,>).Assembly;

            loadContext.LoadFromAssemblyName(_testAssembly.GetName().Name);
            loadContext.FinishLoading();

            Assert.IsTrue(loadContext.LoadedAssemblies.Count() == 1);
            var testAssembly = loadContext.LoadedAssemblies.First();

            var result = new StringBuilder();
            WriteTypes(testAssembly, result);
            Assert.IsTrue(result.ToString() == Properties.Resources.ExpectedOutput);

        }

        private static void WriteTypes(MrAssembly testAssembly, StringBuilder result)
        {
            foreach (var mrType in testAssembly.GetAllTypes())
            {
                result.AppendLine();

                if (mrType.IsPublic)
                {
                    result.Append("public ");
                }

                var classKind = "";
                if (mrType.IsStruct)
                    classKind = "struct";
                else if (mrType.IsClass)
                    classKind = "class";
                else if (mrType.IsInterface)
                    classKind = "interface";
                else if (mrType.IsEnum)
                    classKind = "enum";
                else
                    Assert.IsTrue(false);

                result.Append($"{classKind} {mrType.GetPrettyFullName()}");

                if (mrType.GetBaseType() != null)
                {
                    result.Append($" : {mrType.GetBaseType().GetPrettyFullName()}");
                }

                result.AppendLine();
                result.AppendLine($"    {mrType.Attributes.ToString()}");

                var customAttributes = mrType.GetCustomAttributes();
                foreach (var customAttribute in customAttributes)
                {
                    customAttribute.GetNameAndNamespace(out var name, out var ns);
                    customAttribute.GetArguments(out var fixedArguments, out var namedArguments);

                    if (fixedArguments.IsEmpty && namedArguments.IsEmpty)
                    {
                        result.AppendLine($"    [{name}]");
                    }
                    else
                    {
                        var allArguments = fixedArguments
                                                .Select(fa => $"{fa.Item2.ToString()}")
                                                .Union(namedArguments.Select(na => $"{na.Item1}={na.Item2}"));

                        result.AppendLine($"    [{name}({string.Join(", ", allArguments)})]");
                    }
                }

                var interfaces = mrType.GetInterfaces();

                foreach (var iface in interfaces)
                {
                    result.AppendLine($"    {iface.GetPrettyFullName()}");
                }


                mrType.GetMethodsAndConstructors(out var methods, out var constructors);

                foreach (var constructor in constructors)
                {
                    result.Append($"    {constructor.DeclaringType.GetPrettyFullName()}(");

                    var parameters = constructor.GetParameters();
                    WriteParameters(parameters, result);
                    result.AppendLine(")");
                }

                foreach (var property in mrType.GetProperties())
                {
                    var propertyName = property.GetName();

                    MrType itemPropertyType = null;
                    if (propertyName == "Item")
                    {
                        itemPropertyType = property.GetItemType(publicishOnly:true);
                    }

                    if (itemPropertyType == null)
                    {
                        result.Append($"    {property.GetPropertyType().GetPrettyFullName()} {propertyName} {{ get; ");
                        if (property.Setter != null)
                        {
                            result.Append($"set; ");
                        }
                        result.AppendLine("}");
                    }
                    else
                    {
                        result.AppendLine($"    {property.GetPropertyType().GetPrettyFullName()} this.[{itemPropertyType}]");
                    }
                }

                foreach (var ev in mrType.GetEvents())
                {
                    result.AppendLine($"    {ev.GetEventType().GetPrettyFullName()} {ev.GetName()} {{ add; remove; }}");
                }

                foreach (var method in methods)
                {
                    result.Append("    ");
                    WriteMethodAttributes(method.MethodDefinition.Attributes, result);

                    result.Append($"{method.ReturnType} {method.GetName()}(");
                    var parameters = method.GetParameters();
                    WriteParameters(parameters, result);
                    result.AppendLine(")");
                }


            }
        }

        private static void WriteMethodAttributes(MethodAttributes attributes, StringBuilder result)
        {
            if (attributes.HasFlag(MethodAttributes.Private))
            {
                result.Append("private ");
            }

            if (attributes.HasFlag(MethodAttributes.Family) && !attributes.HasFlag(MethodAttributes.Public))
            {
                result.Append("protected ");
            }
            if (attributes.HasFlag(MethodAttributes.Static))
            {
                result.Append("static ");
            }

        }

        private static void WriteParameters(ImmutableArray<MrParameter> parameters, StringBuilder result)
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                if (i != 0)
                {
                    result.Append(", ");
                }

                result.Append($"{parameter.GetParameterType().GetFullName()} {parameter.GetParameterName()}");
            }
        }

        private string AssemblyPathFromName(string assemblyName)
        {
            if(_testAssembly.GetName().Name == assemblyName)
            {
                return _testAssembly.Location;
            }

            return null;
        }
    }
}
