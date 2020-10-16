using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace UnitTestSampleAssembly
{
    abstract public class Class1<T1, T2, T3>
        where T1 : Stream
        where T3 : class
    {
        static Class1() { return; }

        [TestAttribute]
        public T1 PublicT1WriteableProperty { get; set; }
        static private T2 StaticPrivateT2Property { get; }
        internal XmlReader InternalXmlReaderProperty { get; }

        public int this[int i] { get { return i; } }

        public Class1<T1, T2, T3> Self => this;

        [TestAttribute]
        static public string StaticPublicStringMethod2(string arg1, int arg2) { return ""; }
        protected virtual void ProtectedVirtualVoidMethod0() { return; }
        internal virtual void InternalVirtualVoidMethod0() { return; }
        protected abstract void ProtectedAbstractVoidMethod0();
        void PrivateMethod0() { return; }

        [TestAttribute]
        public event EventHandler<EventArgs> PublicEvent;
        internal event EventHandler<EventArgs> InternalEvent;

        [TestAttribute]
        static int PrivateStaticIntField = 1;
        const int PrivateConstIntField = 2;
        public int PublicIntField;
    }

    public class Class2<T1>
        : Class1<T1, string, string>, IPublicInterface
        where T1 : Stream
    {
        protected Class2() { }

        public string PublicInterfaceStringMethod() { throw new NotImplementedException(); }

        [MTAThread]
        public string PublicStringAttributeMethod0() { return ""; }

        internal Class1<Stream, float, string> InternalClass1OfStreamFloatStringProperty { get; }

        public List<string> PublicListOfStringProperty { get; }

        public Class1<Stream, T1, string> PartiallyClosedTypeMethod() { return null; }

        protected override void ProtectedVirtualVoidMethod0() { base.ProtectedVirtualVoidMethod0(); }
        protected override void ProtectedAbstractVoidMethod0() { return; }
        internal override void InternalVirtualVoidMethod0() { base.InternalVirtualVoidMethod0(); }
    }

    [Obsolete("Test attribute")]
    class Class3 : Class2<Stream>
    {
    }

    public class ExplicitInterfacesClass : IPublicInterface, IInternalInterface
    {
        string IInternalInterface.InternalInterfaceStringMethod() { return ""; }

        string IPublicInterface.PublicInterfaceStringMethod() { return ""; }
    }

    public interface IPublicInterface
    {
        string PublicInterfaceStringMethod();
    }

    internal interface IInternalInterface
    {
        string InternalInterfaceStringMethod();
    }

    internal struct Struct1
    {
        public int PublicIntField;
        public double PublicDoubleField;
        public int PublicIntMethod0() { return 0; }
    }

    public enum ByteEnum : byte { Field1, Field2 }

    public enum DefaultEnum { Field1, Field2 }

    [Flags]
    public enum FlagsEnum { Field1, Field2 }

    [AttributeUsage(AttributeTargets.All)]
    public class TestAttribute : Attribute
    {
    }

}
