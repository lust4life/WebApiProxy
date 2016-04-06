
using System.Collections.Generic;


namespace WebApiProxy.Core.Models
{
    public class ConstantDefinition
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Value { get; set; }
        public string Description { get; set; }

        /// <summary>
        /// get customattribute
        /// </summary>
        public IEnumerable<string> CustomAttributes { get; set; }
    }
}
