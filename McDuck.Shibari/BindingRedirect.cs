using System;
using System.Xml.Linq;

namespace McDuck.Shibari
{
    class BindingRedirect : IEquatable<BindingRedirect>
    {
        public Version Min { get; set; }
        public Version Max { get; set; }
        public Version New { get; set; }

        public bool Equals(BindingRedirect other)
            => other != null
               && Equals(other.Min, Min)
               && Equals(other.Max, Max)
               && Equals(other.New, New);

        public override bool Equals(object obj)
            => obj is BindingRedirect other && Equals(other);

        public override int GetHashCode()
        {
            int hash = 13;
            hash = (hash * 7) + Min.GetHashCode();
            hash = (hash * 7) + Max.GetHashCode();
            hash = (hash * 7) + New.GetHashCode();
            return hash;
        }

        public XElement ToXElement(XNamespace ns)
            => new XElement(ns + "bindingRedirect", new XAttribute("oldVersion", $"{Min}-{Max}"), new XAttribute("newVersion", New.ToString()));

    }
}