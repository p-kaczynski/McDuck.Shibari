using System;
using System.Linq;
using System.Reflection;

namespace McDuck.Shibari
{
    class AssemblyDesc : IEquatable<AssemblyDesc>
    {
        public string Name { get; set; }
        public Version Version { get; set; }
        public string PublicKeyToken { get; set; }

        public AssemblyDesc(string name, string version, string publicKeyToken)
        {
            Name = name;
            Version = Version.Parse(version);
            PublicKeyToken = publicKeyToken;
        }
        public AssemblyDesc(Assembly assembly)
        {
            Name = assembly.GetName().Name;
            Version = assembly.GetName().Version;
            PublicKeyToken = ToStringBase16(assembly.GetName().GetPublicKeyToken());
        }

        public AssemblyDesc(AssemblyName assemblyName)
        {
            Name = assemblyName.Name;
            Version = assemblyName.Version;
            PublicKeyToken = ToStringBase16(assemblyName.GetPublicKeyToken());
        }

        public bool Equals(AssemblyDesc other)
            => other != null
               && string.Equals(other.Name, Name, StringComparison.OrdinalIgnoreCase)
               && Equals(other.Version, Version)
               && string.Equals(other.PublicKeyToken, PublicKeyToken, StringComparison.OrdinalIgnoreCase);

        public override bool Equals(object obj)
            => obj is AssemblyDesc other && Equals(other);

        public override int GetHashCode()
        {
            int hash = 13;
            hash = (hash * 7) + Name.GetHashCode();
            hash = (hash * 7) + Version.GetHashCode();
            hash = (hash * 7) + PublicKeyToken.GetHashCode();
            return hash;
        }

        public override string ToString()
            => $"[{Name}] ({Version})";

        private static string ToStringBase16(byte[] buffer)
            => buffer.Aggregate(string.Empty,
                (result, item) => result + item.ToString("X2"));
    }
}