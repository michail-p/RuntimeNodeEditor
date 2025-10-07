using System;
using System.Collections.Generic;

public static class NodeRegistry
{
    public static List<Type> Nodes = new()
    {
        typeof(FloatNode),
        typeof(MessageNode),
        typeof(MultiplyNode)
    };
}