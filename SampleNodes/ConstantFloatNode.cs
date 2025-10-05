using UnityEngine;

[CreateNodeMenu("Runtime Samples/Constant Float")]
public class ConstantFloatNode : XNode.Node
{
    [Output(backingValue = ShowBackingValue.Always)] public float Value = 1f;

    public override object GetValue(XNode.NodePort port)
    {
        if (port.fieldName == nameof(Value))
        {
            Debug.Log($"ConstantFloatNode.GetValue() returning: {Value}");
            return Value;
        }
        return null;
    }
}
