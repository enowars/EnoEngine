using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Json.Schema;
using Json.Schema.Generation;

namespace EnoCore.Schema
{
    [AttributeUsage(AttributeTargets.Property)]
    public class Description : Attribute, IAttributeHandler
    {
        public string Value { get; }

        public Description(string value)
        {
            Value = value;
        }

        void IAttributeHandler.AddConstraints(SchemaGeneratorContext context)
        {
            var attribute = context.Attributes.OfType<Description>().FirstOrDefault();
            if (attribute == null) return;

            if (!context.Type.IsNumber()) return;

            context.Intents.Add(new DescriptionIntent(attribute.Value));
        }
    }

    public class DescriptionIntent : ISchemaKeywordIntent
    {
        public string Context { get; private set; }

        public DescriptionIntent(string context)
        {
            Context = context;
        }

        public void Replace(int hashCode, string newContext)
        {
            var hc = Context.GetHashCode();
            if (hc == hashCode)
                Context = newContext;
        }

        public void Apply(JsonSchemaBuilder builder) => builder.Description(this.Context);

        //public override bool Equals(object obj) => !ReferenceEquals(null, obj);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = GetType().GetHashCode();
                hashCode = (hashCode * 397) ^ Context.GetHashCode();
                return hashCode;
            }
        }
    }
}
