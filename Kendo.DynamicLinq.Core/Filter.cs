using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Reflection;
using System.ComponentModel;
using System.Globalization;

namespace Kendo.DynamicLinq
{
    /// <summary>
    /// Represents a filter expression of Kendo DataSource.
    /// </summary>
    [DataContract]
    public class Filter
    {
        private static IFormatProvider _customeFormatProvider = CultureInfo.InvariantCulture;
        /// <summary>
        /// Gets or sets the custom value format provider,e.g. a CultureInfo, for value conversion. Default=CultureInfo.InvariantCulture
        /// </summary>
        public static IFormatProvider CustomValueFormatProvider 
        { 
            get
            { 
                return _customeFormatProvider; 
            }
            set
            { 
                _customeFormatProvider = value; 
            } 
        }
        
        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="T:Kendo.DynamicLinq.Filter"/> is case sensitive.
        /// </summary>
        /// <value><c>true</c> if case sensitive; otherwise, <c>false</c>.</value>
        [DataMember(Name = "casesensitive", IsRequired = false, EmitDefaultValue = false)]
        public bool CaseSensitive { get; set; }
        
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
                if (type == typeof(System.String) && !CaseSensitive)
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
                if (type == typeof(System.String) && !CaseSensitive)
                    return String.Format("{0}.ToLower().{1}(@{2})", Field, comparison, index);
                else
                    return String.Format("{0}.{1}(@{2})", Field, comparison, index);
            }

            if (Operator == "eq" && type == typeof(System.String) && !CaseSensitive) 
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
                    //var value = Convert.ChangeType(Value, fieldType);
                    object value = null;
                    if (Value != null && fieldType == typeof(System.String) && !CaseSensitive && (Operator == "eq" || Operator == "contains" || Operator == "doesnotcontain" || Operator == "startswith" || Operator == "endswith"))
                    {
                        value = Value.ToLower();
                    }
                    else if (Value != null && (fieldType == typeof(System.DateTime) || fieldType == typeof(System.DateTime?)))
                    {
                        DateTime temp;
                        if (DateTime.TryParse(Value, CustomValueFormatProvider, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AllowWhiteSpaces, out temp))
                            value = temp;
                        else
                            value = ChangeType(Value, fieldType, CustomValueFormatProvider);
                    }
                    else 
                    {
                        value = ChangeType(Value, fieldType, CustomValueFormatProvider);
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
        
        /// <see cref="http://aspalliance.com/852_CodeSnip_ConvertChangeType_Wrapper_that_Handles_Nullable_Types"/>
        /// <summary>
        /// Returns an Object with the specified Type and whose value is equivalent to the specified object.
        /// </summary>
        /// <param name="value">An Object that implements the IConvertible interface.</param>
        /// <param name="conversionType">The Type to which value is to be converted.</param>
        /// <returns>An object whose Type is conversionType (or conversionType's underlying type if conversionType
        /// is Nullable&lt;&gt;) and whose value is equivalent to value. -or- a null reference, if value is a null
        /// reference and conversionType is not a value type.</returns>
        /// <remarks>
        /// This method exists as a workaround to System.Convert.ChangeType(Object, Type) which does not handle
        /// nullables as of version 2.0 (2.0.50727.42) of the .NET Framework. The idea is that this method will
        /// be deleted once Convert.ChangeType is updated in a future version of the .NET Framework to handle
        /// nullable types, so we want this to behave as closely to Convert.ChangeType as possible.
        /// This method was written by Peter Johnson at:
        /// http://aspalliance.com/author.aspx?uId=1026.
        /// </remarks>
        public static object ChangeType(object value, Type conversionType, IFormatProvider formatProvider = null)
        {
            // Note: This if block was taken from Convert.ChangeType as is, and is needed here since we're
            // checking properties on conversionType below.
            if (conversionType == null)
            {
                throw new ArgumentNullException(nameof(conversionType));
            } // end if

            // If it's not a nullable type, just pass through the parameters to Convert.ChangeType

            if (conversionType.IsGenericType &&
              conversionType.GetGenericTypeDefinition().Equals(typeof(Nullable<>)))
            {
                // It's a nullable type, so instead of calling Convert.ChangeType directly which would throw a
                // InvalidCastException (per http://weblogs.asp.net/pjohnson/archive/2006/02/07/437631.aspx),
                // determine what the underlying type is
                // If it's null, it won't convert to the underlying type, but that's fine since nulls don't really
                // have a type--so just return null
                // Note: We only do this check if we're converting to a nullable type, since doing it outside
                // would diverge from Convert.ChangeType's behavior, which throws an InvalidCastException if
                // value is null and conversionType is a value type.
                if (value == null)
                {
                    return null;
                } // end if

                // It's a nullable type, and not null, so that means it can be converted to its underlying type,
                // so overwrite the passed-in conversion type with this underlying type
                NullableConverter nullableConverter = new NullableConverter(conversionType);
                conversionType = nullableConverter.UnderlyingType;
            } // end if

            // Now that we've guaranteed conversionType is something Convert.ChangeType can handle (i.e. not a
            // nullable type), pass the call on to Convert.ChangeType
            return Convert.ChangeType(value, conversionType, formatProvider);
        }
    }

}
