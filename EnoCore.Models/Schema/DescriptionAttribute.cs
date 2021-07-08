using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Json.Schema;
using Json.Schema.Generation;

namespace EnoCore.Models.Schema
{
    // Can be removed after https://github.com/gregsdennis/json-everything/issues/141
    [AttributeUsage(AttributeTargets.Property)]
    public class Description : Attribute, IAttributeHandler
    {
        public string Value { get; }

        public Description(string value)
        {
            Value = value;
        }

        public void AddConstraints(SchemaGeneratorContext context)
        {
            var attribute = context.Attributes.OfType<Description>().FirstOrDefault();
            if (attribute == null) return;

            context.Intents.Add(new DescriptionIntent(attribute.Value));
        }
    }

    public class DescriptionIntent : ISchemaKeywordIntent
    {
        public string Description { get; private set; }

        public DescriptionIntent(string description)
        {
            Description = description;
        }

        public void Apply(JsonSchemaBuilder builder) => builder.Description(this.Description);
    }
}
