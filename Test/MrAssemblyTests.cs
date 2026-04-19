using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MiddleweightReflection;

namespace MRUnitTests
{
    [TestClass]
    public class MrAssemblyTests
    {
        static MrLoadContext _loadContext;
        static MrAssembly _testAssembly;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            var sampleAssembly = typeof(UnitTestSampleAssembly.Class1<,,>).Assembly;

            _loadContext = new MrLoadContext();
            _loadContext.AssemblyPathFromName = (string assemblyName) =>
            {
                if (sampleAssembly.GetName().Name == assemblyName)
                    return sampleAssembly.Location;
                return null;
            };

            _loadContext.LoadFromAssemblyName(sampleAssembly.GetName().Name);
            _loadContext.FinishLoading();

            _testAssembly = _loadContext.LoadedAssemblies.First();
        }

        [TestMethod]
        public void TestVersion()
        {
            Assert.AreEqual(new Version(1, 2, 3, 0), _testAssembly.Version);
        }

        [TestMethod]
        public void TestCulture()
        {
            Assert.AreEqual(string.Empty, _testAssembly.Culture);
        }

        [TestMethod]
        public void TestPublicKey()
        {
            Assert.IsTrue(_testAssembly.PublicKey.IsEmpty);
        }

        [TestMethod]
        public void TestFlags()
        {
            Assert.AreEqual((System.Reflection.AssemblyFlags)0, _testAssembly.Flags);
        }

        [TestMethod]
        public void TestHashAlgorithm()
        {
            Assert.AreEqual("Sha1", _testAssembly.HashAlgorithm.ToString());
        }

        [TestMethod]
        public void TestGetAssemblyName()
        {
            var assemblyName = _testAssembly.GetAssemblyName();
            Assert.AreEqual("MRUnitTestSampleAssembly", assemblyName.Name);
            Assert.AreEqual(new Version(1, 2, 3, 0), assemblyName.Version);
            Assert.AreEqual("", assemblyName.CultureName);
            Assert.AreEqual(0, assemblyName.GetPublicKeyToken().Length);
        }

        [TestMethod]
        public void TestMvid()
        {
            // Cross-check MR's MVID against System.Reflection's MVID for the same assembly
            var reflectionMvid = typeof(UnitTestSampleAssembly.Class1<,,>).Assembly.ManifestModule.ModuleVersionId;
            Assert.AreNotEqual(System.Guid.Empty, _testAssembly.Mvid);
            Assert.AreEqual(reflectionMvid, _testAssembly.Mvid);
        }

        [TestMethod]
        public void TestModuleName()
        {
            Assert.AreEqual("MRUnitTestSampleAssembly.dll", _testAssembly.ModuleName);
        }

        [TestMethod]
        public void TestGetReferencedAssemblies()
        {
            var refs = _testAssembly.GetReferencedAssemblies();
            var refNames = refs.Select(r => r.Name).OrderBy(n => n).ToArray();

            Assert.AreEqual(5, refs.Length);
            CollectionAssert.AreEqual(
                new[] {
                    "System.Collections",
                    "System.Runtime",
                    "System.Runtime.InteropServices",
                    "System.Threading",
                    "System.Xml.ReaderWriter"
                },
                refNames);
        }

        [TestMethod]
        public void TestGetReferencedAssemblyVersions()
        {
            var refs = _testAssembly.GetReferencedAssemblies();
            foreach (var r in refs)
            {
                Assert.AreEqual(new Version(8, 0, 0, 0), r.Version,
                    $"Referenced assembly '{r.Name}' has unexpected version");
            }
        }

        [TestMethod]
        public void TestGetCustomAttributes()
        {
            var attrs = _testAssembly.GetCustomAttributes();
            Assert.AreEqual(7, attrs.Length);

            var attrFullNames = attrs.Select(a =>
            {
                a.GetNameAndNamespace(out var name, out var ns);
                return $"{ns}.{name}";
            }).ToArray();

            CollectionAssert.AreEqual(
                new[] {
                    "System.Runtime.CompilerServices.CompilationRelaxationsAttribute",
                    "System.Runtime.CompilerServices.RuntimeCompatibilityAttribute",
                    "System.Diagnostics.DebuggableAttribute",
                    "System.Reflection.AssemblyTrademarkAttribute",
                    "System.Runtime.InteropServices.ComVisibleAttribute",
                    "System.Runtime.InteropServices.GuidAttribute",
                    "System.Runtime.Versioning.TargetFrameworkAttribute"
                },
                attrFullNames);
        }

        [TestMethod]
        public void TestCustomAttributeComVisible()
        {
            var attrs = _testAssembly.GetCustomAttributes();
            var comVisible = attrs.First(a =>
            {
                a.GetNameAndNamespace(out var name, out _);
                return name == "ComVisibleAttribute";
            });

            comVisible.GetArguments(out var fixedArgs, out var namedArgs);
            Assert.AreEqual(1, fixedArgs.Length);
            Assert.AreEqual(false, fixedArgs[0].Value);
            Assert.AreEqual(0, namedArgs.Length);
        }

        [TestMethod]
        public void TestCustomAttributeGuid()
        {
            var attrs = _testAssembly.GetCustomAttributes();
            var guid = attrs.First(a =>
            {
                a.GetNameAndNamespace(out var name, out _);
                return name == "GuidAttribute";
            });

            guid.GetArguments(out var fixedArgs, out var namedArgs);
            Assert.AreEqual(1, fixedArgs.Length);
            Assert.AreEqual("65e7c87b-7e7b-4eef-a6aa-5b8cd2f9d265", fixedArgs[0].Value);
            Assert.AreEqual(0, namedArgs.Length);
        }

        [TestMethod]
        public void TestCustomAttributeTargetFramework()
        {
            var attrs = _testAssembly.GetCustomAttributes();
            var tf = attrs.First(a =>
            {
                a.GetNameAndNamespace(out var name, out _);
                return name == "TargetFrameworkAttribute";
            });

            tf.GetArguments(out var fixedArgs, out var namedArgs);
            Assert.AreEqual(1, fixedArgs.Length);
            Assert.AreEqual(".NETCoreApp,Version=v8.0", fixedArgs[0].Value);
            Assert.AreEqual(1, namedArgs.Length);
            Assert.AreEqual("FrameworkDisplayName", namedArgs[0].Name);
            Assert.AreEqual(".NET 8.0", namedArgs[0].Value);
        }

        [TestMethod]
        public void TestCustomAttributeRuntimeCompatibility()
        {
            var attrs = _testAssembly.GetCustomAttributes();
            var rc = attrs.First(a =>
            {
                a.GetNameAndNamespace(out var name, out _);
                return name == "RuntimeCompatibilityAttribute";
            });

            rc.GetArguments(out var fixedArgs, out var namedArgs);
            Assert.AreEqual(0, fixedArgs.Length);
            Assert.AreEqual(1, namedArgs.Length);
            Assert.AreEqual("WrapNonExceptionThrows", namedArgs[0].Name);
            Assert.AreEqual(true, namedArgs[0].Value);
        }

        [TestMethod]
        public void TestFakeAssemblyReturnsDefaults()
        {
            var fakeContext = new MrLoadContext();
            fakeContext.FakeTypeRequired += (sender, args) => { };

            fakeContext.LoadFromAssemblyName("NonExistentAssembly");
            fakeContext.FinishLoading();

            var fakeAssembly = fakeContext.LoadedAssemblies.First();
            Assert.IsTrue(fakeAssembly.IsFakeAssembly);

            Assert.IsNull(fakeAssembly.Version);
            Assert.IsNull(fakeAssembly.Culture);
            Assert.IsTrue(fakeAssembly.PublicKey.IsEmpty);
            Assert.IsNull(fakeAssembly.GetAssemblyName());
            Assert.IsTrue(fakeAssembly.GetReferencedAssemblies().IsEmpty);
            Assert.IsTrue(fakeAssembly.GetCustomAttributes().IsEmpty);
            Assert.AreEqual(System.Guid.Empty, fakeAssembly.Mvid);
            Assert.IsNull(fakeAssembly.ModuleName);
        }

        [TestMethod]
        public void TestFunctionPointerTypeEquality()
        {
            // Get Class1 which has function pointer properties with different parameter types
            _loadContext.TryFindMrType("UnitTestSampleAssembly.Class1`3", out var class1);
            Assert.IsNotNull(class1);

            var allProps = class1.GetProperties(publicishOnly: false);

            // FunctionPointerProperty3: delegate*<int, double, float>
            var fp3 = allProps.FirstOrDefault(p => p.GetName() == "FunctionPointerProperty3");
            Assert.IsNotNull(fp3, "FunctionPointerProperty3 not found");
            var fp3Type = fp3.GetPropertyType();

            // FunctionPointerProperty4: delegate*<int, long, float>
            var fp4 = allProps.FirstOrDefault(p => p.GetName() == "FunctionPointerProperty4");
            Assert.IsNotNull(fp4, "FunctionPointerProperty4 not found");
            var fp4Type = fp4.GetPropertyType();

            // Same calling convention and arity but different parameter types
            // Before the fix, these would incorrectly be equal
            Assert.IsTrue(fp3Type.IsFunctionPointer);
            Assert.IsTrue(fp4Type.IsFunctionPointer);
            Assert.AreNotEqual(fp3Type, fp4Type, 
                $"Function pointers with different parameter types should not be equal: {fp3Type.GetPrettyFullName()} vs {fp4Type.GetPrettyFullName()}");
            Assert.IsFalse(fp3Type == fp4Type);

            // Same type should still be equal to itself
            Assert.AreEqual(fp3Type, fp3Type);
        }

        [TestMethod]
        public void TestModifiedTypeEquality()
        {
            // Get ModifiedTypes1 which has out, ref, array parameters
            _loadContext.TryFindMrType("UnitTestSampleAssembly.Class1`3", out var class1);
            Assert.IsNotNull(class1);

            class1.GetMethodsAndConstructors(out var methods, out _, publicishOnly: true);
            var modifiedTypes1 = methods.FirstOrDefault(m => m.GetName() == "ModifiedTypes1");
            Assert.IsNotNull(modifiedTypes1, "ModifiedTypes1 not found");

            var params1 = modifiedTypes1.GetParameters();
            Assert.AreEqual(4, params1.Length);

            // p1 is out string (reference), p2 is ref string (reference), p3 is string[], p4 is out string[]
            var p1Type = params1[0].GetParameterType(); // string&
            var p2Type = params1[1].GetParameterType(); // string&
            var p3Type = params1[2].GetParameterType(); // string[]
            var p4Type = params1[3].GetParameterType(); // string[]&

            // out string and ref string should be the same type (both are string&)
            Assert.AreEqual(p1Type, p2Type, "out string and ref string should be equal (both are string&)");

            // string[] and string& should be different
            Assert.AreNotEqual(p1Type, p3Type, "string& and string[] should not be equal");

            // string[] and string[]& should be different
            Assert.AreNotEqual(p3Type, p4Type, "string[] and string[]& should not be equal");

            // Verify GetHashCode consistency: equal types must have same hash
            Assert.AreEqual(p1Type.GetHashCode(), p2Type.GetHashCode(),
                "Equal types must have the same hash code");

            // Unequal types should ideally have different hashes (not guaranteed but very likely)
            Assert.AreNotEqual(p1Type.GetHashCode(), p3Type.GetHashCode(),
                "string& and string[] should have different hash codes");
        }

        [TestMethod]
        public void TestFunctionPointerHashCodeConsistency()
        {
            _loadContext.TryFindMrType("UnitTestSampleAssembly.Class1`3", out var class1);
            var allProps = class1.GetProperties(publicishOnly: false);

            var fp3Type = allProps.First(p => p.GetName() == "FunctionPointerProperty3").GetPropertyType();
            var fp4Type = allProps.First(p => p.GetName() == "FunctionPointerProperty4").GetPropertyType();

            // Different function pointers should have different hash codes
            Assert.AreNotEqual(fp3Type.GetHashCode(), fp4Type.GetHashCode(),
                "Different function pointer types should have different hash codes");

            // Same type should have consistent hash
            Assert.AreEqual(fp3Type.GetHashCode(), fp3Type.GetHashCode());
        }
    }
}
