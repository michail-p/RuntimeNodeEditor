[CreateNodeMenu("Runtime Samples/Multiply")]
public class MultiplyNode : XNode.Node
{
    [Input] public float A;
    [Input] public float B = 1f;
    [Output] public float Result;

    public override object GetValue(XNode.NodePort port)
    {
        if (port.fieldName != nameof(Result))
            return null;

        float a = GetInputValue(nameof(A), A);
        float b = GetInputValue(nameof(B), B);

        return a * b;
    }
}
