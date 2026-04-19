using System;
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
        string _name;
        public string Name
        {
            get
            {
                if (string.IsNullOrEmpty(_name) && Reader != null)
                {
                    try
                    {
                        _name = Reader.GetString(Reader.GetAssemblyDefinition().Name);
                    }
                    // Ignore bad metadata that doesn't have a valid assembly name
                    catch { }
                }
                return _name;
            }
            private set { _name = value; }
        }
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

        /// <summary>
        /// Gets the assembly version from the assembly definition metadata.
        /// </summary>
        public Version Version
        {
            get
            {
                if (IsFakeAssembly || Reader == null)
                {
                    return null;
                }
                return Reader.GetAssemblyDefinition().Version;
            }
        }

        /// <summary>
        /// Gets the culture string (e.g. "en-US") or empty for culture-neutral assemblies.
        /// </summary>
        public string Culture
        {
            get
            {
                if (IsFakeAssembly || Reader == null)
                {
                    return null;
                }
                var cultureHandle = Reader.GetAssemblyDefinition().Culture;
                return cultureHandle.IsNil ? string.Empty : cultureHandle.AsString(Reader);
            }
        }

        /// <summary>
        /// Gets the public key of the assembly, or an empty array if not strong-named.
        /// </summary>
        public ImmutableArray<byte> PublicKey
        {
            get
            {
                if (IsFakeAssembly || Reader == null)
                {
                    return ImmutableArray<byte>.Empty;
                }
                var blobHandle = Reader.GetAssemblyDefinition().PublicKey;
                return blobHandle.IsNil ? ImmutableArray<byte>.Empty : Reader.GetBlobContent(blobHandle);
            }
        }

        /// <summary>
        /// Gets the assembly flags.
        /// </summary>
        public System.Reflection.AssemblyFlags Flags
        {
            get
            {
                if (IsFakeAssembly || Reader == null)
                {
                    return default;
                }
                return Reader.GetAssemblyDefinition().Flags;
            }
        }

        /// <summary>
        /// Gets the hash algorithm used by this assembly.
        /// </summary>
        public AssemblyHashAlgorithm HashAlgorithm
        {
            get
            {
                if (IsFakeAssembly || Reader == null)
                {
                    return default;
                }
                return Reader.GetAssemblyDefinition().HashAlgorithm;
            }
        }

        /// <summary>
        /// Gets the AssemblyName, including public key token and processor architecture.
        /// </summary>
        public AssemblyName GetAssemblyName()
        {
            if (IsFakeAssembly || Reader == null)
            {
                return null;
            }
            return Reader.GetAssemblyDefinition().GetAssemblyName();
        }

        /// <summary>
        /// Gets the Module Version ID (MVID), a GUID that uniquely identifies this build of the module.
        /// </summary>
        public Guid Mvid
        {
            get
            {
                if (IsFakeAssembly || Reader == null)
                {
                    return Guid.Empty;
                }

                try
                {
                    var moduleDef = Reader.GetModuleDefinition();
                    return Reader.GetGuid(moduleDef.Mvid);
                }
                catch { return Guid.Empty; }
            }
        }

        /// <summary>
        /// Gets the module name (typically the filename of the assembly).
        /// </summary>
        public string ModuleName
        {
            get
            {
                if (IsFakeAssembly || Reader == null)
                {
                    return null;
                }

                try
                {
                    var moduleDef = Reader.GetModuleDefinition();
                    return Reader.GetString(moduleDef.Name);
                }
                catch { return null; }
            }
        }

        /// <summary>
        /// Gets the assemblies referenced by this assembly.
        /// </summary>
        public ImmutableArray<AssemblyName> GetReferencedAssemblies()
        {
            if (IsFakeAssembly || Reader == null)
            {
                return ImmutableArray<AssemblyName>.Empty;
            }

            var builder = ImmutableArray.CreateBuilder<AssemblyName>();
            foreach (var handle in Reader.AssemblyReferences)
            {
                try
                {
                    var reference = Reader.GetAssemblyReference(handle);
                    builder.Add(reference.GetAssemblyName());
                }
                // Skip malformed references
                catch { }
            }
            return builder.ToImmutable();
        }


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


        /// <summary>
        /// Gets the custom attributes applied at the assembly level.
        /// </summary>
        public ImmutableArray<MrCustomAttribute> GetCustomAttributes()
        {
            if (IsFakeAssembly || Reader == null)
            {
                return ImmutableArray<MrCustomAttribute>.Empty;
            }

            var assemblyAttributeHandles = Reader.GetAssemblyDefinition().GetCustomAttributes();
            var customAttributes = new List<MrCustomAttribute>(assemblyAttributeHandles.Count);
            foreach (var handle in assemblyAttributeHandles)
            {
                try
                {
                    customAttributes.Add(new MrCustomAttribute(handle, null, this));
                }
                // Skip attributes that can't be decoded
                catch
                {
                }
            }
            return customAttributes.ToImmutableArray();
        }


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
