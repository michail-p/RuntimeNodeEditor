using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class RuntimeNodeReflection
{
    public static List<FieldDescriptor> GetFieldList(Type type)
    {
        var descriptors = new List<FieldDescriptor>();
        while (type != null && type != typeof(XNode.Node))
        {
            if (!new HashSet<Type>().Add(type))
                break;

            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (!IsFieldSerializable(field))
                    continue;

                descriptors.Add(new FieldDescriptor(field));
            }

            type = type.BaseType;
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

public class FieldDescriptor
{
    public FieldInfo Field;
    public XNode.Node.InputAttribute Input;
    public XNode.Node.OutputAttribute Output;

    public FieldDescriptor(FieldInfo field)
    {
        Field = field;
        Input = field.GetCustomAttribute<XNode.Node.InputAttribute>();
        Output = field.GetCustomAttribute<XNode.Node.OutputAttribute>();
    }
}