﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace MRUnitTests.Properties {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "16.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("MRUnitTests.Properties.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to 
        ///class &lt;Module&gt;
        ///    NotPublic
        ///
        ///public class UnitTestWinRTComponent.WinRTComponent : System.Object
        ///    AutoLayout, AnsiClass, Class, Public, Sealed, BeforeFieldInit
        ///    [MarshalingBehaviorAttribute]
        ///    [ThreadingAttribute]
        ///    [VersionAttribute(16777216)]
        ///    [ActivatableAttribute(16777216)]
        ///    UnitTestWinRTComponent.IWinRTComponentClass
        ///    UnitTestWinRTComponent.WinRTComponent()
        ///    System.Collections.Generic.IList&lt;System.String&gt; StringList { get; set; }
        ///
        ///class UnitTestWinRTComponent.&lt;WinRT [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string ExpectdProjectedWinRT {
            get {
                return ResourceManager.GetString("ExpectdProjectedWinRT", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to 
        ///class &lt;Module&gt;
        ///    NotPublic
        ///
        ///public class UnitTestSampleAssembly.Class1&lt;T1,T2,T3&gt; : System.Object
        ///    AutoLayout, AnsiClass, Class, Public, Abstract
        ///    [DefaultMemberAttribute(Item)]
        ///    UnitTestSampleAssembly.Class1&lt;T1,T2,T3&gt;()
        ///    T1 PublicT1WriteableProperty { get; set; }
        ///    System.Int32 this.[System.Int32]
        ///    UnitTestSampleAssembly.Class1&lt;T1,T2,T3&gt; Self { get; }
        ///    System.EventHandler&lt;System.EventArgs&gt; PublicEvent { add; remove; }
        ///    static System.String StaticPublicStringMethod2(Syst [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string ExpectedOutput {
            get {
                return ResourceManager.GetString("ExpectedOutput", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to 
        ///class &lt;Module&gt;
        ///    NotPublic
        ///
        ///class UnitTestWinRTComponent.&lt;CLR&gt;WinRTComponent : System.Object
        ///    AutoLayout, AnsiClass, Class, Sealed, SpecialName, BeforeFieldInit
        ///    [MarshalingBehaviorAttribute]
        ///    [ThreadingAttribute]
        ///    [VersionAttribute(16777216)]
        ///    [ActivatableAttribute(16777216)]
        ///    UnitTestWinRTComponent.IWinRTComponentClass
        ///    UnitTestWinRTComponent.&lt;CLR&gt;WinRTComponent()
        ///    System.Collections.Generic.IList&lt;System.String&gt; StringList { get; set; }
        ///
        ///public class UnitTestWinRTC [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string ExpectedUnprojectedWinRT {
            get {
                return ResourceManager.GetString("ExpectedUnprojectedWinRT", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Byte[].
        /// </summary>
        internal static byte[] UnitTestWinRTComponent {
            get {
                object obj = ResourceManager.GetObject("UnitTestWinRTComponent", resourceCulture);
                return ((byte[])(obj));
            }
        }
    }
}