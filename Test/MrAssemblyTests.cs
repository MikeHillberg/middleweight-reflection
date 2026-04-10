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
        }
    }
}
