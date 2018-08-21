using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Reflection;

namespace Kendo.DynamicLinq
{
    /// <summary>
    /// Represents a filter expression of Kendo DataSource.
    /// </summary>
    [DataContract]
    public class Filter
    {
        /// <summary>
        /// Gets or sets the name of the sorted field (property). Set to <c>null</c> if the <c>Filters</c> property is set.
        /// </summary>
        [DataMember(Name = "field")]
        public string Field { get; set; }

        /// <summary>
        /// Gets or sets the filtering operator. Set to <c>null</c> if the <c>Filters</c> property is set.
        /// </summary>
        [DataMember(Name = "operator")]
        public string Operator { get; set; }

        /// <summary>
        /// Gets or sets the string representation of filtering value. Set to <c>null</c> if the <c>Filters</c> property is set.
        /// </summary>
        [DataMember(Name = "value")]
        public String Value { get; set; }
        
        /// <summary>
        /// Gets or sets the filtering value.
        /// </summary>
        /// <value>The value.</value>
        public Object ValueConverted { get; set; }
        
        /// <summary>
        /// Gets or sets the filtering logic. Can be set to "or" or "and". Set to <c>null</c> unless <c>Filters</c> is set.
        /// </summary>
        [DataMember(Name = "logic")]
        public string Logic { get; set; }

        /// <summary>
        /// Gets or sets the child filter expressions. Set to <c>null</c> if there are no child expressions.
        /// </summary>
        [DataMember(Name = "filters")]
        public IEnumerable<Filter> Filters { get; set; }

        /// <summary>
        /// Mapping of Kendo DataSource filtering operators to Dynamic Linq
        /// </summary>
        private static readonly IDictionary<string, string> operators = new Dictionary<string, string>
        {
            {"eq", "="},
            {"neq", "!="},
            {"lt", "<"},
            {"lte", "<="},
            {"gt", ">"},
            {"gte", ">="},
            {"isnull", "="},
            {"isnotnull", "!="},
            {"startswith", "StartsWith"},
            {"endswith", "EndsWith"},
            {"contains", "Contains"},
            {"doesnotcontain", "Contains"},
            {"isempty", ""},
            {"isnotempty", "!" }
        };

        /// <summary>
        /// Get a flattened list of all child filter expressions.
        /// </summary>
        public IList<Filter> All()
        {
            var filters = new List<Filter>();

            Collect(filters);

            return filters;
        }

        private void Collect(IList<Filter> filters)
        {
            if (Filters != null && Filters.Any())
            {
                foreach (Filter filter in Filters)
                {
                    filters.Add(filter);

                    filter.Collect(filters);
                }
            }
            else
            {
                filters.Add(this);
            }
        }

        /// <summary>
        /// Converts the filter expression to a predicate suitable for Dynamic Linq e.g. "Field1 = @1 and Field2.Contains(@2)"
        /// </summary>
        /// <param name="filters">A list of flattened filters.</param>
        public string ToExpression(Type rsType, IList<Filter> filters)
        {
            if (Filters != null && Filters.Any())
            {
                return "(" + String.Join(" " + Logic + " ", Filters.Select(filter => filter.ToExpression(rsType, filters)).ToArray()) + ")";
            }

            if (ValueConverted == null)
                ValueConverted = ToValue(rsType);
            
            int index = filters.IndexOf(this);

            string comparison = operators[Operator];

            Type type = GetType(rsType);

            if (Operator == "doesnotcontain")
            {
                if (type == typeof(System.String))
                    return String.Format("!{0}.ToLower().{1}(@{2})", Field, comparison, index);
                else
                    return String.Format("!{0}.{1}(@{2})", Field, comparison, index);
            }

            if (Operator == "isnotnull" || Operator == "isnull")
            {
                if (type == typeof(System.String))
                {
                    if (Operator == "isnotnull")
                        return String.Format("!String.IsNullOrEmpty({0})", Field);
                    else
                        return String.Format("String.IsNullOrEmpty({0})", Field);
                }
                return String.Format("{0} {1} null", Field, comparison);
            }

            if (Operator == "isempty" || Operator == "isnotempty")
            {
                return String.Format("{1}String.IsNullOrEmpty({0})", Field, comparison);

            }

            if (comparison == "StartsWith" || comparison == "EndsWith" || comparison == "Contains")
            {
                if (type == typeof(System.String))
                    return String.Format("{0}.ToLower().{1}(@{2})", Field, comparison, index);
                else
                    return String.Format("{0}.{1}(@{2})", Field, comparison, index);
            }

            if (Operator == "eq" && type == typeof(System.String)) 
            {
                return String.Format("{0}.ToLower().Equals(@{1})", Field, index);
            }
            
            return String.Format("{0} {1} @{2}", Field, comparison, index);
        }
        
        public Object ToValue(Type resultType)
        {
            var fields = resultType.GetRuntimeProperties();
            foreach (var field in fields)
                if (field.Name.Equals(Field, StringComparison.OrdinalIgnoreCase))
	            {
	                Type fieldType = field.PropertyType;
                    var value = Convert.ChangeType(Value, fieldType);
                    if (fieldType == typeof(System.String) && (Operator == "eq" || Operator == "contains" || Operator == "doesnotcontain" || Operator == "startswith" || Operator == "endswith"))
                    {
                        value = Value.ToLower();
                    }
                    return value;
	            }
            return null;
        }
        
        public Type GetType(Type resultType)
        {
            var fields = resultType.GetRuntimeProperties();
            foreach (var field in fields)
                if (field.Name.Equals(Field, StringComparison.OrdinalIgnoreCase))
                {
                    return field.PropertyType;
                }
            return null;
        }
    }
}
