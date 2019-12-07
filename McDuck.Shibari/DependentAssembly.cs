using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace McDuck.Shibari
{
    class DependentAssembly : IEquatable<DependentAssembly>
    {
        public string Name { get; set; }
        public string PublicKeyToken { get; set; }

        public HashSet<BindingRedirect> Redirects { get; set; }
        public HashSet<CodeBase> CodeBases { get; set; }

        public bool Equals(DependentAssembly other)
            => other != null
               && string.Equals(other.Name, Name, StringComparison.OrdinalIgnoreCase)
               && string.Equals(other.PublicKeyToken, PublicKeyToken, StringComparison.OrdinalIgnoreCase)
               && Redirects.SetEquals(other.Redirects);

        public override bool Equals(object obj)
            => obj is DependentAssembly other && Equals(other);

        public override int GetHashCode()
        {
            int hash = 13;
            hash = (hash * 7) + Name.GetHashCode();
            hash = (hash * 7) + PublicKeyToken.GetHashCode();
            return hash;
        }

        public XElement ToXElement(XNamespace ns)
            => new XElement(ns + "dependentAssembly",
                new XElement(ns + "assemblyIdentity",
                    new XAttribute("name", Name),
                    new XAttribute("publicKeyToken", PublicKeyToken),
                    new XAttribute("culture", "neutral")
                ),
                Redirects.OrderBy(r => r.Max).Select(r => r.ToXElement(ns)),
                CodeBases.Select(c => c.ToXElement(ns))
            );
    }
}