using System;

namespace Rpi.Common.Network
{
    /// <summary>
    /// Represents a network interface.
    /// </summary>
    public class Interface
    {
        public string Name { get; }
        public string PhysicalAddress { get; }
        public string InternetAddress { get; }

        public Interface(string name, string physicalAddress, string internetAddress)
        {
            Name = name;
            PhysicalAddress = physicalAddress;
            InternetAddress = internetAddress;
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 31 + (Name ?? String.Empty).GetHashCode();
            hash = hash * 31 + (PhysicalAddress ?? String.Empty).GetHashCode();
            hash = hash * 31 + (InternetAddress ?? String.Empty).GetHashCode();
            return hash;
        }

        public override bool Equals(object obj)
        {
            if ((obj == null) || (!(obj is Interface i)))
                return false;
            return i.GetHashCode() == GetHashCode();
        }
    }
}
