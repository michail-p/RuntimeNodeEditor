using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// Utility for caching reflection metadata about node fields that can be edited at runtime.
internal static class RuntimeNodeReflection
{
    internal struct FieldDescriptor
    {
        public FieldInfo Field;
        public XNode.Node.InputAttribute Input;
        public XNode.Node.OutputAttribute Output;
        public string NicifiedName;

        public FieldDescriptor(FieldInfo field)
        {
            Field = field;
            Input = field.GetCustomAttribute<XNode.Node.InputAttribute>();
            Output = field.GetCustomAttribute<XNode.Node.OutputAttribute>();
            NicifiedName = Nicify(field.Name);
        }

        static string Nicify(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            var chars = new List<char>(name.Length + 8);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (c == '_')
                {
                    chars.Add(' ');
                    continue;
                }

                bool insertSpace = i > 0 && char.IsUpper(c) && char.IsLower(name[i - 1]);
                if (insertSpace)
                    chars.Add(' ');

                chars.Add(i == 0 ? char.ToUpperInvariant(c) : c);
            }

            return new string(chars.ToArray());
        }
    }

    static Dictionary<Type, List<FieldDescriptor>> cache = new();

    internal static IReadOnlyList<FieldDescriptor> GetSerializableFields(Type type)
    {
        if (type == null)
            return Array.Empty<FieldDescriptor>();

        if (!cache.TryGetValue(type, out var list))
        {
            list = BuildFieldList(type);
            cache[type] = list;
        }

        return list;
    }

    static List<FieldDescriptor> BuildFieldList(Type type)
    {
        var descriptors = new List<FieldDescriptor>();
        var processedTypes = new HashSet<Type>();
        Type current = type;
        while (current != null && current != typeof(XNode.Node))
        {
            if (!processedTypes.Add(current))
                break;

            var fields = current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            foreach (var field in fields)
            {
                if (!IsFieldSerializable(field)) continue;
                descriptors.Add(new FieldDescriptor(field));
            }

            current = current.BaseType;
        }

        return descriptors;
    }

    static bool IsFieldSerializable(FieldInfo field)
    {
        if (field.IsStatic)
            return false;

        if (field.IsInitOnly)
            return false;

        if (Attribute.IsDefined(field, typeof(NonSerializedAttribute)))
            return false;

        if (Attribute.IsDefined(field, typeof(HideInInspector)))
            return false;

        if (!field.IsPublic && !Attribute.IsDefined(field, typeof(SerializeField)))
            return false;

        // Skip fields declared on the base Node class (graph, position, ports, etc.)
        if (field.DeclaringType == typeof(XNode.Node))
            return false;

        return true;
    }
}