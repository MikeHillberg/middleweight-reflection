using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;


#pragma warning disable CS0067,CS0649,CS0414

namespace UnitTestSampleAssembly
{
    abstract public class Class1<T1, T2, T3>
        where T1 : Stream
        where T3 : class
    {
        // This doesn't show up in the metadata
        static Class1() { return; }

        [TestAttribute]
        public T1 PublicT1WriteableProperty { get; set; }
        static private T2 StaticPrivateT2Property { get; }
        internal XmlReader InternalXmlReaderProperty { get; }

        public int this[int i] { get { return i; } }

        public Class1<T1, T2, T3> Self => this;
        public Class1<Stream,int,object> ClosedSelf { get; }

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

        public object ObjectProperty { get; }
    }

    public class Class2<T1>
        : Class1<T1, string, string>, IPublicInterface
        where T1 : Stream
    {
        protected Class2()  { }

        public string PublicInterfaceStringMethod() { throw new NotImplementedException(); }

        [MTAThread]
        public string PublicStringAttributeMethod0() { return ""; }

        internal Class1<Stream, float, string> InternalClass1OfStreamFloatStringProperty { get; }

        public List<string> PublicListOfStringProperty { get; }

        public Class1<Stream, T1, string> PartiallyClosedTypeMethod() { return null; }

        protected override void ProtectedVirtualVoidMethod0() { base.ProtectedVirtualVoidMethod0(); }
        protected override void ProtectedAbstractVoidMethod0() { return; }
        internal override void InternalVirtualVoidMethod0() { base.InternalVirtualVoidMethod0(); }
        protected internal void ProtectedInternalMethod() { }
    }

    [Obsolete("Test attribute")]
    public class Class3 : Class2<Stream>
    {
        public class NestedInClass3
        {
            internal NestedInClass3() { }
            internal int InternalProp { get; }
            private int PrivateField;
            internal void InternalMethod() { return; }
            private event EventHandler PrivateEvent;
            public int PropertyWithDifferentAccessors { get; private set; }
        }

        public NestedInClass3 PropOfNestedType { get; }

        private class PrivateNestedInClass3
        {
        }

        protected class ProtectedNestedInClass3
        {
        }
        internal class InternalNestedInClass3
        {
        }


        protected internal class ProtectedInternalNestedInClass3
        {
        }
    }

    // A full closed type
    public class ClosedType1 : Class1<Stream, int, string>
    {
        protected override void ProtectedAbstractVoidMethod0()
        {
            throw new NotImplementedException();
        }
    }

    // Same as above, but closing with different type arguments
    public class ClosedType2 : Class1<Stream, double, string>
    {
        protected override void ProtectedAbstractVoidMethod0()
        {
            throw new NotImplementedException();
        }
    }

    // An open type with one parameter
    public class OpenTypeA<T>
    {
        public T Value { get; set; }
    }

    // Same open type as above but two parameters
    public class OpenTypeA<T,U>
    {
        public T TValue { get; set; }
        public U UValue { get; set; }
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
