﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MiddleweightReflection
{
    /// <summary>
    /// Primary object for a set of MRAssembly objects. Use the Load methods on this class to load assemblies.
    /// Note: the loaded assemblies won't be available until FinishLoading is called, after which no further assemblies
    /// can be loaded.
    /// </summary>
    public class MrLoadContext
    {
        bool _loading = true;

        public MrLoadContext()
        {
        }

        /// <summary>
        /// A MRLoadContext into which can be loaded assemblies. If useWinRTProjections is set,
        /// some types in a WinMD will be converted, for example IVector to IList.
        /// </summary>
        /// <param name="useWinRTProjections"></param>
        public MrLoadContext(bool useWinRTProjections)
        {
            if (useWinRTProjections)
            {
                MetadataReaderOptions = MetadataReaderOptions.ApplyWindowsRuntimeProjections;
            }
        }

        /// <summary>
        /// Call this after loading assemblies, after which the LoadedAssemblies property will become non-null
        /// </summary>
        public void FinishLoading()
        {
            if (!_loading)
            {
                throw new Exception("Call StartLoading() and load assemblies before calling FinishLoading()");
            }

            _loading = false;

            // All the assemblies need to be loaded before they're initialized. That way we know how 
            // to resolve assembly references.
            // Note that _assembliesToInitialize can change as we iterate through this loop,
            // because new fake assemblies might get generated. So use a for loop rather 
            // than a foreach iterator.

            for (int i = 0; i < _assembliesToInitialize.Count; i++)
            {
                _assembliesToInitialize[i].Initialize();
            }
        }

        public delegate string AssemblyPathFromNameCallback(string assemblyName);
        public AssemblyPathFromNameCallback AssemblyPathFromName { get; set; }
        //public delegate string AssemblyPathFromNameCallback(string assemblyName);


        /// <summary>
        /// Get a type instance given its name. Throws if the type isn't in a loaded assembly.
        /// </summary>
        public MrType GetTypeFromAssembly(string fullTypeName, string assemblyName)
        {
            if (!_loadedAssemblies.TryGetValue(assemblyName, out var assembly))
            {
                if (!_implicitAssemblies.TryGetValue(assemblyName, out assembly))
                {
                    throw new Exception($"Can't find '{assemblyName}'");
                }
            }

            if (assembly.IsFakeAssembly)
            {
                assembly.LoadContext.RaiseFakeTypeRequired(fullTypeName, assemblyName, out var replacementType);

                // If above returns null, the below GetTypeFromName will create a fake type
                if (replacementType != null)
                {
                    return replacementType;
                }
            }

            return assembly.GetTypeFromName(fullTypeName);
        }

        internal MrType CreateFakeType(string fullTypeName)
        {
            this.RaiseFakeTypeRequired(fullTypeName, null, out var replacementType);
            return replacementType;
        }

        /// <summary>
        /// Find a type by name from any loaded assembly
        /// </summary>
        public MrType GetType(string fullTypeName)
        {
            if (TryFindMrType(fullTypeName, out var mrType))
            {
                return mrType;
            }

            throw new Exception($"Couldn't find in any loaded assembly: '{fullTypeName}'");
        }

        /// <summary>
        /// Try to find a type in a specific assembly
        /// </summary>
        public bool TryFindMrType(string fullTypeName, MetadataReader reader, out MrType mrType)
        {
            mrType = null;

            foreach (var assembly in _loadedAssemblies.Values.Union(_implicitAssemblies.Values))
            {
                if (assembly.Reader == reader)
                {
                    return assembly.TryGetType(fullTypeName, out mrType);
                }
            }

            return false;
        }

        /// <summary>
        /// Try to find a type in any loaded assembly
        /// </summary>
        public bool TryFindMrType(string fullTypeName, out MrType mrType)
        {
            mrType = null;

            // Convert e.g. Byte[] into Byte
            fullTypeName = MrType.GetUnmodifiedTypeName(fullTypeName, out var arrayRank, out var isReference, out var isPointer);

            foreach (var assembly in _loadedAssemblies.Values.Union(_implicitAssemblies.Values))
            {
                if (assembly.TryGetType(fullTypeName, out mrType))
                {
                    // Convert back if necessary, e.g. Byte into Byte[]
                    if (arrayRank != null || isReference || isPointer)
                    {
                        mrType = MrType.Clone(mrType, arrayRank, isReference, isPointer);
                    }

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Assemblies loaded by a Load method. This will be null until FinishLoading() is called.
        /// </summary>
        public ICollection<MrAssembly> LoadedAssemblies
        {
            get
            {
                if (_loading)
                    return null;

                return _loadedAssemblies.Values;
            }
        }
        Dictionary<string, MrAssembly> _loadedAssemblies = new Dictionary<string, MrAssembly>();

        // Assemblies that weren't explicitly loaded with a Load() call, but were referenced
        // by a loaded assembly.
        Dictionary<string, MrAssembly> _implicitAssemblies = new Dictionary<string, MrAssembly>();

        /// <summary>
        /// Load an assembly given its location. If already loaded, return that.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public MrAssembly LoadAssemblyFromPath(string path)
        {
            var reader = CreateReaderFromPath(path);
            if (reader == null)
            {
                // Not an assembly
                return null;
            }

            var name = reader.GetString(reader.GetAssemblyDefinition().Name);

            if (_loadedAssemblies.TryGetValue(name, out var assembly))
            {
                return assembly;
            }

            return LoadFromReader(reader, name, path, implicitLoad: false);
        }

        /// <summary>
        /// Load an assembly from memory. If already loaded, return that.
        /// </summary>
        /// <param name="buffer">Assembly contents</param>
        /// <param name="path">Location of the assembly (optional and not used, just used to return from the Location property</param>
        public MrAssembly LoadAssemblyFromBytes(byte[] buffer, string path)
        {
            var reader = CreateReaderFromBytes(buffer);
            if(reader == null)
            {
                // Not an assembly
                return null;
            }

            var name = reader.GetString(reader.GetAssemblyDefinition().Name);

            if (_loadedAssemblies.TryGetValue(name, out var assembly))
            {
                return assembly;
            }

            return LoadFromReader(reader, name, path, implicitLoad: false);
        }

        /// <summary>
        /// Load an assembly from memory. If already loaded, return that.
        /// </summary>
        public MrAssembly LoadAssemblyFromBytes(byte[] buffer)
        {
            return LoadAssemblyFromBytes(buffer, null);
        }

        /// <summary>
        /// Load given the assembly name. If already loaded, return that.
        /// </summary>
        /// <param name="requestedName"></param>
        /// <returns></returns>
        public MrAssembly LoadFromAssemblyName(string requestedName)
        {
            return LoadFromAssemblyName(requestedName, implicitLoad: false);
        }

        /// <summary>
        /// Load given the assembly name. If implicitLoad, it's not from a Load method but
        /// part of resolving a loaded method.
        /// </summary>
        /// <param name="requestedName"></param>
        /// <param name="implicitLoad"></param>
        /// <returns></returns>
        internal MrAssembly LoadFromAssemblyName(string requestedName, bool implicitLoad)
        {
            if (_loadedAssemblies.TryGetValue(requestedName, out var assembly))
            {
                return assembly;
            }

            if (_implicitAssemblies.TryGetValue(requestedName, out assembly))
            {
                // If we'd loaded this before implicitly, but now it's being loaded explicitly,
                // move it to the right cache
                if (!implicitLoad)
                {
                    _loadedAssemblies[requestedName] = assembly;
                    _implicitAssemblies.Remove(requestedName);
                }
                return assembly;
            }

            CreateReaderFromAssemblyName(requestedName, out var reader, out var path);
            return LoadFromReader(reader, requestedName, path, implicitLoad);
        }

        /// <summary>
        /// Load an assembly from its MetadataReader. If reader is null, create a fake assembly.
        /// </summary>
        private MrAssembly LoadFromReader(MetadataReader reader, string name, string path, bool implicitLoad)
        {
            if (!_loading && !implicitLoad)
            {
                throw new Exception("Once FinishLoading() has been called, no more assemblies can be loaded");
            }

            MrAssembly newAssembly;
            if (reader == null)
            {
                newAssembly = MrAssembly.CreateFakeAssembly(name, this);
            }
            else
            {
                Debug.Assert(name == reader.GetString(reader.GetAssemblyDefinition().Name) || name == "mscorlib");

                // See if the assembly was already explicitly loaded
                if (_loadedAssemblies.TryGetValue(name, out var loadedAssembly))
                {
                    return loadedAssembly;
                }

                // Or it might have been implicitly loaded
                if (_implicitAssemblies.TryGetValue(name, out loadedAssembly))
                {
                    if (implicitLoad)
                    {
                        return loadedAssembly;
                    }

                    // This assembly was implicitly loaded, now it's being explicitly loaded,
                    // so move it from implicit to explicit.
                    _loadedAssemblies[name] = loadedAssembly;
                    _implicitAssemblies.Remove(name);
                    return loadedAssembly;
                }

                // Prevent stack overflow by making this assembly exist before creating it
                // (which might trigger a cycle of assembly references)
                if (implicitLoad)
                {
                    _implicitAssemblies[name] = null;
                }

                newAssembly = MrAssembly.Create(reader, path, this);
            }

            // Save the assembly
            if (implicitLoad)
            {
                _implicitAssemblies[name] = newAssembly;
            }
            else
            {
                _loadedAssemblies[name] = newAssembly;
            }

            // Remember to initialize this assembly in FinishLoading()
            _assembliesToInitialize.Add(newAssembly);

            return newAssembly;
        }

        List<MrAssembly> _assembliesToInitialize = new List<MrAssembly>();


        /// <summary>
        /// Find an assembly from its name and create a MetadataReader
        /// </summary>
        void CreateReaderFromAssemblyName(string requestedName, out MetadataReader reader, out string location)
        {
            location = null;
            reader = null;

            // Use callback to try and find the assembly
            if (AssemblyPathFromName != null)
            {
                // It's OK for this to return null
                location = AssemblyPathFromName(requestedName);
            }

            if (location == null)
            {
                return;
            }

            // Create a MetadataReader from the path
            reader = CreateReaderFromPath(location);

            // Validate that the name of the assembly is what it's supposed to be
            var name = reader.GetString(reader.GetAssemblyDefinition().Name);
            if (name != requestedName && requestedName != "mscorlib")
            {
                throw new Exception($"Expected assembly name '{requestedName}', actual is '{name}'");
            }

            return;
        }

        /// <summary>
        /// Create the System.Reflection.Metadata MetadataReader give the path to an assembly
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        unsafe MetadataReader CreateReaderFromPath(string path)
        {
            var buffer = File.ReadAllBytes(path);
            return CreateReaderFromBytes(buffer);
        }

        /// <summary>
        /// Create the System.Reflection.Metadata MetadataReader given an in-memory assembly
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        unsafe MetadataReader CreateReaderFromBytes(byte[] buffer)
        {
            try
            {
                var pinnedHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                var headers = new PEHeaders(new MemoryStream(buffer));
                var startOffset = headers.MetadataStartOffset;
                var metaDataStart = (byte*)pinnedHandle.AddrOfPinnedObject() + startOffset;

                return new MetadataReader(metaDataStart, headers.MetadataSize, this.MetadataReaderOptions, null);
            }
            catch (BadImageFormatException)
            {
                // Not a metadata file
                return null;
            }
        }

        internal MetadataReaderOptions MetadataReaderOptions { get; private set; } = MetadataReaderOptions.None;


        public MrType GetTypeFromReference(MetadataReader reader, TypeReferenceHandle referenceHandle)
        {
            var typeReference = reader.GetTypeReference(referenceHandle);
            var scopeEntityHandle = typeReference.ResolutionScope;

            while(scopeEntityHandle.Kind == HandleKind.TypeReference)
            {
                typeReference = reader.GetTypeReference((TypeReferenceHandle)scopeEntityHandle);
                scopeEntityHandle = typeReference.ResolutionScope;
            }

            string name = typeReference.Namespace.IsNil
                ? reader.GetString(typeReference.Name)
                : reader.GetString(typeReference.Namespace) + "." + reader.GetString(typeReference.Name);

            switch (scopeEntityHandle.Kind)
            {
                case HandleKind.AssemblyReference:
                    {
                        var assemblyReferenceHandle = (AssemblyReferenceHandle)scopeEntityHandle;
                        var assemblyReference = reader.GetAssemblyReference(assemblyReferenceHandle);
                        return GetTypeFromAssembly(name, reader.GetString(assemblyReference.Name));
                    }

                case HandleKind.ModuleDefinition:
                    {
                        return GetType(name);
                    }

                case HandleKind.TypeReference:
                    {
                        // Todo: is there a way to get the assembly being referenced? Or is it always the same one?
                        TryFindMrType(name, reader, out var mrType);
                        return mrType;
                    }

                default:
                    throw new NotSupportedException();
            }
        }

        public event EventHandler<FakeTypeRequiredEventArgs> FakeTypeRequired;

        public class FakeTypeRequiredEventArgs : EventArgs
        {
            internal FakeTypeRequiredEventArgs(
                string typeName,
                string assemblyName)
            {
                AssemblyName = assemblyName;
                TypeName = typeName;
            }

            public string AssemblyName { get; }
            public string TypeName { get; }
            public MrType ReplacementType { get; set; }
        }

        private void RaiseFakeTypeRequired(string fullTypeName, string assemblyName, out MrType type)
        {
            type = null;
            if (FakeTypeRequired != null)
            {
                var args = new FakeTypeRequiredEventArgs(fullTypeName, assemblyName);
                FakeTypeRequired(this, args);
                type = args.ReplacementType;
            }
        }

        // Common implementation of operator==
        static internal bool OperatorEquals<T>(T op1, T op2) where T : class
        {
            if (op1 == null)
            {
                return op2 == null;
            }

            return op1.Equals(op2);
        }

        // Common code for the start of every Equals override method
        static internal bool? OverrideEqualsProlog<T>(T op1, T op2) where T : class
        {
            // Optimization: if they're equal, they'll often be the same instance
            if (Object.ReferenceEquals(op1, op2))
            {
                return true;
            }

            if (op2 == null)
            {
                return false;
            }

            return null;
        }


    }
}
