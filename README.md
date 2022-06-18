# middleweight-reflection

MiddleweightReflection is a .Net library that wraps [System.Reflection.Metadata](https://www.nuget.org/packages/System.Reflection.Metadata/).
SRM is a very simple, lightweight library for reading ECMA 335 .Net assemblies and Windows WinMD files.
MiddleweightReflection wraps that to make things easier, though a little heavier.

## Usage

To use the library, load some metadata files into a Context, and then you can enumerate the assemblies,
their types, and their members (including attributes). If it can't find a referenced assembly you can
answer a callback to provide its location. If you don't, it automatically creates a fake assembly
with fake types.

There's a basic example in the [unit test](https://github.com/MikeHillberg/middleweight-reflection/blob/main/Test/UnitTest1.cs).

NuGet: https://www.nuget.org/packages/mikehillberg.middleweightreflection

Note that unless you load .Net assemblies, assemblies such as `System` will be faked.
The faking works pretty well, but for example you won't be able to determine the `T` in `EventHandler<T>`,
because `System.EventHandler~1` will be faked.

But you can supply extra assemblies using the `AssemblyPathFromName` callback.
For example, handling a few interesting cases in .Net Framework:

```cs
var loadContext = new MrLoadContext();
loadContext.AssemblyPathFromName = (requestedName) =>
{
    string location = null;

    if (requestedName == "mscorlib")
    {
        location = (typeof(string).Assembly).Location;
    }
    else if (requestedName == "System")
    {
        location = typeof(NetTcpStyleUriParser).Assembly.Location;
    }
    else if (requestedName == "System.Runtime")
    {
        var mscorlib = (typeof(string).Assembly).Location;
        mscorlib = mscorlib.Substring(0, mscorlib.LastIndexOf('\\'));
        location = $@"{mscorlib}\System.Runtime.dll";
    }
    else
    {
        Debug.WriteLine($"Faking assembly {requestedName}");
    }

    return location;
};
```

## Versions

**1.1.3**
Don't try to guess at the location of .Net assemblies like mscorlib;
fake them by default, or caller can provide them.

**1.1.2**
Added an overload to LoadAssemblyFromBytes to take a path (which is just returned from Assembly.Location)

## Issues

There are some more advanced metadata features not covered yet, but the most notable are:
* Assembly attributes
* Full support for fully qualified assembly names

