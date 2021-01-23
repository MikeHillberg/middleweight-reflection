using MiddleweightReflection;
using MRUnitTests;
using System;
using System.Linq;
using System.Text;

namespace MRDump
{
    class Program
    {
        static void Main(string[] args)
        {
            var loadContext = new MrLoadContext();
            //loadContext.AssemblyPathFromName = (string assemblyName) =>
            //{
            //    return null;
            //};

            loadContext.LoadAssemblyFromPath(args[0]);
            loadContext.FinishLoading();

            var sb = new StringBuilder();
            UnitTest1.WriteTypes(loadContext.LoadedAssemblies.First(), sb);

        }
    }
}
