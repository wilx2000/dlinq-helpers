using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.Extensions.DependencyModel;

namespace Kendo.DynamicLinq
{
    /// <summary>
    /// Describes the result of Kendo DataSource read operation. 
    /// </summary>
    [KnownType("GetKnownTypes")]
    public class DataSourceResult
    {
        /// <summary>
        /// Represents a single page of processed data.
        /// </summary>
        public IEnumerable Data { get; set; }

        /// <summary>
        /// The total number of records available.
        /// </summary>
        public int Total { get; set; }

        /// <summary>
        /// Represents a requested aggregates.
        /// </summary>
        public object Aggregates { get; set; }

        /// <summary>
        /// Used by the KnownType attribute which is required for WCF serialization support
        /// </summary>
        /// <returns></returns>
        private static Type[] GetKnownTypes()
        {
            //var assembly = AppDomain.CurrentDomain
            //.GetAssemblies()
            //.FirstOrDefault(a => a.FullName.StartsWith("DynamicClasses"));


            var assembly = GetReferencingAssemblies("DynamicClass").FirstOrDefault();

            if (assembly == null)
            {
                return new Type[0];
            }
            else
                return assembly.ExportedTypes.ToArray();

            //return assembly.GetTypes()
                           //.Where(t => t.Name.StartsWith("DynamicClass"))
                           //.ToArray();
        }

        public static IEnumerable<Assembly> GetReferencingAssemblies(string assemblyName)
        {
            var assemblies = new List<Assembly>();
            var dependencies = DependencyContext.Default.RuntimeLibraries;
            foreach (var library in dependencies)
            {
                if (library.Name == (assemblyName)
                || library.Dependencies.Any(d => d.Name.StartsWith(assemblyName, StringComparison.Ordinal)))
                {
                    var assembly = Assembly.Load(new AssemblyName(library.Name));
                    assemblies.Add(assembly);
                }
            }
            return assemblies;
        }

        private static bool IsCandidateLibrary(RuntimeLibrary library, String assemblyName)
        {
            return library.Name == (assemblyName)
                || library.Dependencies.Any(d => d.Name.StartsWith(assemblyName, StringComparison.Ordinal));
        }
    }
}
