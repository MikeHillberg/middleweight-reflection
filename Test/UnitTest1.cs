using System;
using System.Collections.Immutable;
using System.IO;
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

        // Load a .Net (not WinRT) assembly using it's name
        [TestMethod]
        public void TestDotNetAssembly()
        {
            var loadContext = LoadDotNetTestAssembly();
            TestMethodHelper(loadContext, Properties.Resources.ExpectedOutput);
        }

        MrLoadContext LoadDotNetTestAssembly()
        {
            var testAssembly = typeof(UnitTestSampleAssembly.Class1<,,>).Assembly;

            var loadContext = new MrLoadContext();
            loadContext.AssemblyPathFromName = (string assemblyName) =>
            {
                if (testAssembly.GetName().Name == assemblyName)
                {
                    return testAssembly.Location;
                }

                return null;
            };

            loadContext.LoadFromAssemblyName(testAssembly.GetName().Name);
            loadContext.FinishLoading();

            return loadContext;
        }

        // Load a WinMD from memory
        [TestMethod]
        public void TestWinMD()
        {
            var loadContext = new MrLoadContext(useWinRTProjections: true);
            loadContext.LoadAssemblyFromBytes(Properties.Resources.UnitTestWinRTComponent);
            loadContext.FinishLoading();

            TestMethodHelper(loadContext, Properties.Resources.ExpectdProjectedWinRT);

            loadContext = new MrLoadContext(useWinRTProjections: false);
            loadContext.LoadAssemblyFromBytes(Properties.Resources.UnitTestWinRTComponent);
            loadContext.FinishLoading();

            TestMethodHelper(loadContext, Properties.Resources.ExpectedUnprojectedWinRT);
        }


        [TestMethod]
        public void TestEquals()
        {
            var loadContext = LoadDotNetTestAssembly();

            // Get the Class1<,,> type from the assembly
            loadContext.TryFindMrType("UnitTestSampleAssembly.Class1`3", out var mrType);

            // Get a closed version of Class1 from the assembly by finding the 'ClosedSelf' property and getting its return type
            var nameofMember = nameof(UnitTestSampleAssembly.Class1<Stream, object, object>.ClosedSelf);
            var prop = mrType.GetProperties().First((p) => p.GetName() == nameofMember);
            var mrType1 = prop.GetPropertyType();

            // Get that closed version of Class1 again. Because of the way SRM works, this will be a new instance of the MRType
            prop = mrType.GetProperties().First((p) => p.GetName() == nameofMember);
            var mrType2 = prop.GetPropertyType();
            Assert.IsFalse(object.ReferenceEquals(mrType1, mrType2));
            Assert.IsTrue(mrType1 == mrType2 && mrType1.Equals(mrType2) && !(mrType1 != mrType2) && mrType1.GetHashCode() == mrType2.GetHashCode());


            var prop1 = mrType1.GetProperties()[0];
            var prop2 = mrType2.GetProperties()[0];
            Assert.IsTrue(prop1 == prop2 && prop1.Equals(prop2) && !(prop1 != prop2) && prop1.GetHashCode() == prop2.GetHashCode());

            mrType1.GetMethodsAndConstructors(out var methods1, out var constructors1);
            mrType2.GetMethodsAndConstructors(out var methods2, out var constructors2);
            var method1 = methods1[0];
            var method2 = methods2[0];
            Assert.IsTrue(method1 == method2 && method1.Equals(method2) && !(method1 != method2) && method1.GetHashCode() == method2.GetHashCode());

            var parameter1 = method1.GetParameters()[0];
            var parameter2 = method2.GetParameters()[0];
            Assert.IsTrue(parameter1 == parameter2 && parameter1.Equals(parameter2) && !(parameter1 != parameter2) && parameter1.GetHashCode() == parameter2.GetHashCode());

            var event1 = mrType1.GetEvents()[0];
            var event2 = mrType2.GetEvents()[0];
            Assert.IsTrue(event1 == event2 && event1.Equals(event2) && !(event1 != event2) && event1.GetHashCode() == event2.GetHashCode() && event1.GetHashCode() == event2.GetHashCode());
        }


        public void TestMethodHelper(MrLoadContext loadContext, string expectedOutput)
        {
            Assert.IsTrue(loadContext.LoadedAssemblies.Count() == 1);
            var testAssembly = loadContext.LoadedAssemblies.First();

            var builder = new StringBuilder();
            WriteTypes(testAssembly, builder);

            Assert.AreEqual(builder.ToString(), expectedOutput);
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
                        itemPropertyType = property.GetItemType(publicishOnly: true);
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

                foreach (var field in mrType.GetFields())
                {
                    if (mrType.IsEnum)
                    {
                        if (!field.IsSpecialName) // Ignore special value__ field
                        {
                            var value = field.GetConstantValue(out var constantTypeCode);
                            result.AppendLine($"    {field.GetName()} = {value},");
                        }
                    }
                    else
                    {
                        result.AppendLine($"    {field.GetFieldType().GetPrettyFullName()} {field.GetName()};");
                    }
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

    }
}
