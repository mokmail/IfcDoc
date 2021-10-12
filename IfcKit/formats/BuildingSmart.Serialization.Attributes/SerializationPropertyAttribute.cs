using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuildingSmart.Serialization.Attributes
{
    public enum SerializationControl { Default, ForceReference, Priority }
    [AttributeUsage(AttributeTargets.Property)]
    public class SerializationPropertyAttribute : Attribute
    {
        public SerializationControl Control { get; set; } = SerializationControl.Default;
        public SerializationPropertyAttribute() { }
        public SerializationPropertyAttribute(SerializationControl control) { Control = control; }
    }
}
