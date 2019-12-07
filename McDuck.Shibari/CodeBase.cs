using System;
using System.Xml.Linq;

namespace McDuck.Shibari
{
    class CodeBase : IEquatable<CodeBase>
    {
        public Version Version { get; set; }
        public string RelativePath { get; set; }

        public bool Equals(CodeBase other)
            => other != null
               && Equals(other.Version, Version)
               && string.Equals(other.RelativePath, RelativePath);

        public override bool Equals(object obj)
            => obj is CodeBase other && Equals(other);

        public override int GetHashCode()
        {
            int hash = 13;
            hash = (hash * 7) + RelativePath.GetHashCode();
            hash = (hash * 7) + Version.GetHashCode();
            return hash;
        }

        public XElement ToXElement(XNamespace ns)
            => new XElement(ns + "codeBase", new XAttribute("version", Version.ToString()), new XAttribute("href", RelativePath));
    }
}