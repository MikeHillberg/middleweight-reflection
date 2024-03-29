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
    public class MrAssembly
    {
        public string Name { get; private set; }
        MrLoadContext _loadContext;
        public MrLoadContext LoadContext { get { return _loadContext; } }

        internal DisassemblingTypeProvider TypeProvider { get; private set; }
        public bool IsFakeAssembly;

        public override string ToString()
        {
            return $"MRAssembly: {(string.IsNullOrEmpty(this.Name) ? this.Location : this.Name)}";
        }

        private MrAssembly(MetadataReader reader, string location, MrLoadContext loadContext)
        {
            this.Location = location;
            this.Reader = reader;
            this._loadContext = loadContext;
        }

        static public MrAssembly Create(MetadataReader reader, string location, MrLoadContext loadContext)
        {
            var mrAssembly = new MrAssembly(reader, location, loadContext);
            return mrAssembly;
        }

        // This must be called after the LoadContext has loaded all the assemblies,
        // so that we know which referent types have to be faked.
        internal void Initialize()
        {
            if (IsFakeAssembly)
            {
                return;
            }

            TypeProvider = new DisassemblingTypeProvider(this);

            var assemblyReferenceHandles = Reader.AssemblyReferences;
            foreach (var assemblyReferenceHandle in assemblyReferenceHandles)
            {
                var assemblyReference = Reader.GetAssemblyReference(assemblyReferenceHandle);
                var referencedAssemblyName = assemblyReference.GetAssemblyName().Name;

                _loadContext.LoadFromAssemblyName(referencedAssemblyName, implicitLoad: true);
            }

            // Get all the type names so that we can look up by name
            EnsureTypesAreLoaded();
        }

        internal static MrAssembly CreateFakeAssembly(string name, MrLoadContext loadContext)
        {
            var mrAssembly = new MrAssembly(null, null, loadContext);
            mrAssembly.IsFakeAssembly = true;
            mrAssembly.Name = name;
            return mrAssembly;
        }

        public string Location { get; private set; }
        public string FullName => Name;


        public MetadataReader Reader;
        unsafe void CreateReaderFromPath(string path)
        {
            Location = path;
            var buffer = File.ReadAllBytes(Location);
            var pinnedHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            var headers = new PEHeaders(new MemoryStream(buffer));
            var startOffset = headers.MetadataStartOffset;
            var metaDataStart = (byte*)pinnedHandle.AddrOfPinnedObject() + startOffset;

            Reader = new MetadataReader(metaDataStart, headers.MetadataSize, this.LoadContext.MetadataReaderOptions, null);
        }

        Dictionary<string, MrType> _nameToMrType;
        public ImmutableArray<MrType> GetAllTypes()
        {
            EnsureTypesAreLoaded();

            if (IsFakeAssembly)
                return ImmutableArray<MrType>.Empty;
            else
                return _nameToMrType.Values.ToImmutableArray<MrType>();
        }


        //public ImmutableArray<MrCustomAttribute> GetCustomAttributes()
        //{
        //    var assemblyAttributeHandles = Reader.GetAssemblyDefinition().GetCustomAttributes();
        //    var customAttributes = GetCustomAttributesFromHandles(assemblyAttributeHandles, this.Reader);
        //    return customAttributes.ToImmutableArray();
        //}


        /// <summary>
        /// Gets the assembly to which this type has been forwarded (or returns null).
        /// </summary>
        string GetForwardingAssemblyForType(string typeName)
        {
            // See if this type has been forward. For example, in .Net Framework,
            // System.Uri used to live in System.Runtime but now lives in mscorlib,
            // so there's a forwarder for it in System.Runtime.

            foreach (var typeHandle in this.Reader.ExportedTypes)
            {
                var exportedType = this.Reader.GetExportedType(typeHandle);
                var name = exportedType.Name.AsString(this.Reader);
                var ns = exportedType.Namespace.AsString(this.Reader);

                if (typeName == $"{ns}.{name}")
                {
                    var assemblyReference = Reader.GetAssemblyReference((AssemblyReferenceHandle)exportedType.Implementation);
                    var referencedAssemblyName = assemblyReference.GetAssemblyName().Name;
                    return referencedAssemblyName;
                }
            }

            return null;
        }

        void EnsureTypesAreLoaded()
        {
            if (_nameToMrType != null || IsFakeAssembly)
            {
                return;
            }

            var typeDefinitions = Reader.TypeDefinitions;
            _nameToMrType = new Dictionary<string, MrType>(typeDefinitions.Count);
            foreach (var typeDefinitionHandle in Reader.TypeDefinitions)
            {
                var mrType = MrType.CreateFromTypeDefinition(typeDefinitionHandle, this);
                _nameToMrType[mrType.GetFullName()] = mrType;
            }
        }


        internal MrType GetTypeFromName(string fullName)
        {
            if (_nameToMrType != null && _nameToMrType.TryGetValue(fullName, out var type))
            {
                return type;
            }

            // For fake assemblies, create a fake type
            // bugbug: When loading with WinRT projections turned on I can't find 
            // EventRegistrationToken in System.Runtime.InteropServices.WindowsRuntime assembly
            if (IsFakeAssembly || fullName == "System.Runtime.InteropServices.WindowsRuntime.EventRegistrationToken")
            {
                type = MrType.CreateFakeType(fullName, this);
                if (_nameToMrType == null)
                {
                    _nameToMrType = new Dictionary<string, MrType>();
                }
                _nameToMrType[fullName] = type;
                return type;
            }
            else
            {
                // The type isn't in this assembly, but there may be a forwarder for it
                var forwardingAssembly = GetForwardingAssemblyForType(fullName);
                if (forwardingAssembly != null)
                {
                    // Look up the type at that forwarding address
                    return _loadContext.GetTypeFromAssembly(fullName, forwardingAssembly);
                }
            }

            throw new Exception("Type not found");
        }

        Dictionary<TypeDefinitionHandle, MrType> _typeCache = new Dictionary<TypeDefinitionHandle, MrType>();

        /// <summary>
        /// Keep a cache of types in this assembly to avoid allocation perf overhead.
        /// This is called from MrType, which keeps its own cache of PrimitiveTypeCode types.
        /// </summary>
        internal MrType GetFromCacheOrCreate(TypeDefinitionHandle typeDefinitionHandle, Func<MrType> createType)
        {
            if (_typeCache.TryGetValue(typeDefinitionHandle, out var type))
            {
                return type;
            }

            lock (_typeCache)
            {
                if (_typeCache.TryGetValue(typeDefinitionHandle, out type))
                {
                    return type;
                }

                type = createType();
                _typeCache[typeDefinitionHandle] = type;
                return type;
            }
        }

        Dictionary<TypeDefinitionHandle, MrType> _typeDefinitonHandleToMrType = new Dictionary<TypeDefinitionHandle, MrType>();
        public MrType GetWrapper(TypeDefinitionHandle typeDefinitionHandle)
        {
            if (_typeDefinitonHandleToMrType.TryGetValue(typeDefinitionHandle, out var mrType))
            {
                return mrType;
            }

            throw new Exception("Update");
        }



        public MrType GetTypeFromEntityHandle(EntityHandle baseTypeEntityHandle, TypeDefinition typeDefinition)
        {
            var provider = new DisassemblingTypeProvider(this);

            switch (baseTypeEntityHandle.Kind)
            {
                case HandleKind.TypeDefinition:
                    {
                        var baseTypeDefinitionHandle = (TypeDefinitionHandle)baseTypeEntityHandle;
                        //var baseTypeHandleWrapper = GetTypeFromEntityHandle(typeDefinition, baseTypeDefinitionHandle);
                        var baseTypeHandleWrapper = MrType.CreateFromTypeDefinition(baseTypeDefinitionHandle, this);
                        return baseTypeHandleWrapper;
                    }

                //case HandleKind.TypeDefinition:
                //    type = GetTypeOfTypeDef((TypeDefinitionHandle)token, out isNoPiaLocalType, isContainingType: false);
                //    break;

                case HandleKind.TypeSpecification:
                    {
                        var typeSpecification = Reader.GetTypeSpecification((TypeSpecificationHandle)baseTypeEntityHandle);


                        var genericTypeParameters = typeDefinition
                            .GetGenericParameters()
                            .Select(h => MrType.CreateFromGenericParameterHandle(h, this))
                            .ToImmutableArray();

                        var context = new MRGenericContext(genericTypeParameters, ImmutableArray<MrType>.Empty);

                        var mrType = typeSpecification.DecodeSignature(provider, context);
                        return mrType;
                    }


                case HandleKind.TypeReference:
                    {
                        var referencedTypeDefinitionHandleWrapper = _loadContext.GetTypeFromReference(Reader, (TypeReferenceHandle)baseTypeEntityHandle);
                        return referencedTypeDefinitionHandleWrapper;
                    }

                default:
                    throw new Exception("Unknown entity type");
            }

        }

        internal bool TryGetType(string fullName, out MrType mrType)
        {
            if (_nameToMrType == null)
            {
                mrType = null;
                return false;
            }

            return _nameToMrType.TryGetValue(fullName, out mrType);
        }

        static internal List<MrCustomAttribute> GetCustomAttributesFromHandles(
                CustomAttributeHandleCollection customAttributeHandles,
                MrType declaringType)
        {
            var customAttributes = new List<MrCustomAttribute>(customAttributeHandles.Count);
            foreach (var customAttributeHandle in customAttributeHandles)
            {
                var customAttribute = new MrCustomAttribute(customAttributeHandle, declaringType, declaringType.Assembly);
                customAttributes.Add(customAttribute);
            }

            return customAttributes;
        }

        public override bool Equals(object obj)
        {
            var other = obj as MrAssembly;
            var prolog = MrLoadContext.OverrideEqualsProlog(this, other);
            if (prolog != null)
            {
                return (bool)prolog;
            }

            return this.Reader == other.Reader;
        }

        public static bool operator ==(MrAssembly operand1, MrAssembly operand2)
        {
            return MrLoadContext.OperatorEquals(operand1, operand2);
        }

        public static bool operator !=(MrAssembly operand1, MrAssembly operand2)
        {
            return !(operand1 == operand2);
        }

        public override int GetHashCode()
        {
            if (this.Reader != null)
            {
                // Fake types don't have a Reader
                return this.Reader.GetHashCode();
            }
            else
            {
                return this.Name.GetHashCode();
            }
        }
    }

}
