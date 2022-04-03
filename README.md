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

## Versions

**1.1.2**
Added an overload to LoadAssemblyFromBytes to take a path (which is just returned from Assembly.Location)

## Issues

There are some more advanced metadata features not covered yet, but the most notable are:
* Assembly attributes
* Full support for fully qualified assembly names

