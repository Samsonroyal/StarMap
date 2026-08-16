using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

var path = args[0];
AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    var candidate = Path.Combine(AppContext.BaseDirectory, name.Name + ".dll");
    return File.Exists(candidate) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate) : null;
};
var assembly = Assembly.LoadFrom(path);
Console.WriteLine("REFERENCES");
foreach (var reference in assembly.GetReferencedAssemblies()) Console.WriteLine($"  {reference}");
var patterns = args.Skip(1).ToArray();
foreach (var type in assembly.GetExportedTypes().Where(t => patterns.Length == 0 || patterns.Any(p => t.FullName?.Contains(p, StringComparison.OrdinalIgnoreCase) == true)))
{
    Console.WriteLine($"TYPE {type.FullName}");
    foreach (var constructor in type.GetConstructors()) Console.WriteLine($"  CTOR {constructor}");
    foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
    {
        if (member.MemberType is MemberTypes.Method or MemberTypes.Property or MemberTypes.Field)
            Console.WriteLine($"  {member.MemberType} {member}");
    }
}
