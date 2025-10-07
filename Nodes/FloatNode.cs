public class FloatNode : XNode.Node
{
    [Output(backingValue = ShowBackingValue.Always)] public float Value = 1f;

    public override object GetValue(XNode.NodePort port)
    {
        if (port.fieldName == nameof(Value))
            return Value;

        return null;
    }
}
