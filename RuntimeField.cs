using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XNode;
using TMPro;
using System.Threading.Tasks;
using System.Collections.Generic;

[RequireComponent(typeof(RectTransform))]
class RuntimeField : UIBehaviour
{
    RuntimeNode _node;
    FieldDescriptor _descriptor;
    NodePort _associatedPort;


    public void Initialize(RuntimeNode host, FieldDescriptor descriptor)
    {
        _node = host;
        _descriptor = descriptor;
        _associatedPort = host.Node != null ? host.Node.GetPort(descriptor.Field.Name) : null;

        BuildLayout();
        _ = CreateControl();
    }

    void BuildLayout()
    {
        var rect = (RectTransform)transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, 28f);

        var layout = gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.spacing = 8f;
        layout.padding = new RectOffset(4, 4, 2, 2);

        var image = gameObject.AddComponent<Image>();
        image.sprite = FindFirstObjectByType<RuntimeNodeEditor>().CustomBG != null ? FindFirstObjectByType<RuntimeNodeEditor>().CustomBG : null;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 64;
        image.color = new Color32(55, 55, 55, 200);

        var _label = Instantiate(UIManager.Instance.LabelPrefab, transform).GetComponent<TextMeshProUGUI>();
        _label.alignment = TextAlignmentOptions.MidlineLeft;
        _label.text = _descriptor.Field.Name;
    }

    async Task CreateControl()
    {
        switch (_descriptor.Field.FieldType)
        {
            case Type t when t == typeof(string) || t == typeof(int) || t == typeof(float):
                CreateInputField(_descriptor.Field.FieldType);
                break;
            case Type t when t == typeof(bool):
                CreateBoolField();
                break;
            case Type t when t == typeof(Vector3):
                CreateVectorField();
                break;
            case Type t when t.IsEnum:
                CreateEnumField();
                break;
            case Type t when t == typeof(Slot):
                await CreateSlotField();
                break;
            case Type t when t == typeof(GameObject):
                CreateGameObjectField();
                break;
        }
    }

    void CreateInputField(Type inputType)
    {
        var input = Instantiate(UIManager.Instance.InputPrefab, transform).GetComponent<TMP_InputField>();

        if (inputType == typeof(string))
            input.contentType = TMP_InputField.ContentType.Standard;
        else if (inputType == typeof(int))
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
        else if (inputType == typeof(float))
            input.contentType = TMP_InputField.ContentType.DecimalNumber;

        if (inputType == typeof(string))
            input.text = GetValue<string>() ?? string.Empty;
        else if (inputType == typeof(int))
            input.text = GetValue<int>().ToString(CultureInfo.InvariantCulture);
        else if (inputType == typeof(float))
            input.text = GetValue<float>().ToString(CultureInfo.InvariantCulture);
        else
            input.text = string.Empty;

        input.onEndEdit.AddListener(async param =>
        {
            if (inputType == typeof(string))
            {
                string textValue = param ?? string.Empty;
                _descriptor.Field.SetValue(_node.Node, textValue);
                input.SetTextWithoutNotify(textValue);
            }
            else if (inputType == typeof(int))
            {
                if (!int.TryParse(param, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedValue))
                    parsedValue = GetValue<int>();

                _descriptor.Field.SetValue(_node.Node, parsedValue);
                input.SetTextWithoutNotify(parsedValue.ToString(CultureInfo.InvariantCulture));
            }
            else if (inputType == typeof(float))
            {
                if (!float.TryParse(param, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsedValue))
                    parsedValue = GetValue<float>();

                _descriptor.Field.SetValue(_node.Node, parsedValue);
                input.SetTextWithoutNotify(parsedValue.ToString(CultureInfo.InvariantCulture));
            }

            _node.OnFieldValueChanged();
            await GameManager.Instance.Save();
        });
    }

    void CreateBoolField()
    {
        var toggleGO = Instantiate(UIManager.Instance.TogglePrefab, transform);

        toggleGO.transform.Find("Background/Label").GetComponent<TextMeshProUGUI>().text = _descriptor.Field.Name;
        toggleGO.transform.Find("Background/Checkmark/Label").GetComponent<TextMeshProUGUI>().text = _descriptor.Field.Name;

        var toggle = toggleGO.GetComponent<Toggle>();
        toggle.isOn = GetValue<bool>();

        toggle.onValueChanged.AddListener(async (param) =>
        {
            _descriptor.Field.SetValue(_node.Node, param);
            _node.OnFieldValueChanged();
            await GameManager.Instance.Save();
        });
    }

    void CreateVectorField()
    {
        var vectorGO = Instantiate(UIManager.Instance.Vector3Prefab, transform);

        vectorGO.transform.Find("Label").GetComponent<TextMeshProUGUI>().text = _descriptor.Field.Name;

        // Get all input fields at once
        TMP_InputField[] inputs = {
            vectorGO.transform.Find("Values/X").GetComponent<TMP_InputField>(),
            vectorGO.transform.Find("Values/Y").GetComponent<TMP_InputField>(),
            vectorGO.transform.Find("Values/Z").GetComponent<TMP_InputField>()
        };

        var currentVectorStruct = GetValue<SVector3>();
        Vector3 currentVector = currentVectorStruct;

        inputs[0].text = currentVector.x.ToString(CultureInfo.InvariantCulture);
        inputs[1].text = currentVector.y.ToString(CultureInfo.InvariantCulture);
        inputs[2].text = currentVector.z.ToString(CultureInfo.InvariantCulture);

        // Add listeners with component indexes
        for (int i = 0; i < inputs.Length; i++)
        {
            int componentIndex = i;
            inputs[componentIndex].onEndEdit.AddListener(async (param) =>
            {
                var currentValueStruct = GetValue<SVector3>();
                Vector3 currentValue = currentValueStruct;
                if (!float.TryParse(param, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsedValue))
                    parsedValue = componentIndex == 0 ? currentValue.x : (componentIndex == 1 ? currentValue.y : currentValue.z);

                var updatedValue = componentIndex switch
                {
                    0 => new SVector3(parsedValue, currentValue.y, currentValue.z),
                    1 => new SVector3(currentValue.x, parsedValue, currentValue.z),
                    _ => new SVector3(currentValue.x, currentValue.y, parsedValue)
                };

                _descriptor.Field.SetValue(_node.Node, updatedValue);
                inputs[componentIndex].SetTextWithoutNotify(parsedValue.ToString(CultureInfo.InvariantCulture));
                _node.OnFieldValueChanged();
                await GameManager.Instance.Save();
            });
        }
    }

    void CreateEnumField()
    {
        var enumGO = Instantiate(UIManager.Instance.DropDownPrefab, transform);

        var dropdown = enumGO.GetComponent<TMP_Dropdown>();
        var label = enumGO.transform.Find("Label").GetComponent<TextMeshProUGUI>();

        label.text = _descriptor.Field.Name;

        var enumValue = GetValue<string>() ?? string.Empty;
        var enumType = enumValue.GetEnumType();

        List<string> options = new();
        foreach (var value in Enum.GetValues(enumType))
            options.Add(value.ToString().Replace("_", " "));
        dropdown.AddOptions(options);

        dropdown.SetValueWithoutNotify(Array.IndexOf(Enum.GetNames(enumType), enumValue));
        dropdown.RefreshShownValue();

        dropdown.onValueChanged.AddListener(async (param) =>
        {
            var names = Enum.GetNames(enumType);
            param = Mathf.Clamp(param, 0, names.Length - 1);
            _descriptor.Field.SetValue(_node.Node, names[param]);

            _node.OnFieldValueChanged();
            await GameManager.Instance.Save();
        });
    }

    async Task CreateSlotField()
    {
        var slotGO = Instantiate(UIManager.Instance.CompactSlotPrefab, transform);

        slotGO.transform.Find("Label").GetComponent<TextMeshProUGUI>().text = _descriptor.Field.Name;
        var slotUI = slotGO.GetComponent<Slot>();
        var portValue = GetValue<Content>();
        slotUI.Content.Type = portValue.Type;

        if (!portValue.IsEmpty)
            if (portValue.IsAsset)
                await slotUI.AddAsset(portValue.AssetPath);
            else if (portValue.IsArtifact)
                await slotUI.AddArtifact(portValue.ArtifactID);

        slotUI.OnAssetChanged.AddListener(async (param) =>
        {
            portValue.AssetPath = param;
            _node.OnFieldValueChanged();
            await GameManager.Instance.Save();
        });

        slotUI.OnArtifactChanged.AddListener(async (param) =>
        {
            portValue.ArtifactID = param.ID;
            _node.OnFieldValueChanged();
            await GameManager.Instance.Save();
        });
    }

    void CreateGameObjectField()
    {
        var GO = Instantiate(UIManager.Instance.LabelPrefab, transform);
        GO.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineLeft;
        GO.GetComponent<TextMeshProUGUI>().text = $"• {_descriptor.Field.Name}";
    }

    public T GetValue<T>() => GetValue<T>(out _);
    public T GetValue<T>(out bool success)
    {
        success = false;

        object raw = _descriptor.Field.GetValue(_node.Node);
        if (raw == null || raw is T)
        {
            success = true;
            return (T)raw;
        }

        try
        {
            Type target = typeof(T);

            // String
            if (target == typeof(string))
            {
                success = true;
                return (T)(object)raw.ToString();
            }

            // Enum
            if (target.IsEnum)
            {
                success = true;
                if (raw is string s)
                    return (T)Enum.Parse(target, s, true);
                return (T)Enum.ToObject(target, raw);
            }

            // Primitives (int, float, bool)
            success = true;
            return (T)Convert.ChangeType(raw, target, CultureInfo.InvariantCulture);
        }
        catch
        {
            UIManager.Instance.Message($"Cannot convert '{_descriptor.Field.Name}' from {raw.GetType().Name} to {typeof(T).Name}");
            return default;
        }
    }

    public void ShouldShow()
    {
        XNode.Node.InputAttribute inputAttr = _descriptor.Input;
        XNode.Node.OutputAttribute outputAttr = _descriptor.Output;

        if (inputAttr != null)
            switch (inputAttr.backingValue)
            {
                case XNode.Node.ShowBackingValue.Never:
                    gameObject.SetActive(false);
                    break;
                case XNode.Node.ShowBackingValue.Unconnected:
                    if (_associatedPort != null && _associatedPort.IsConnected)
                        gameObject.SetActive(false);
                    break;
            }

        if (outputAttr != null && outputAttr.backingValue == XNode.Node.ShowBackingValue.Never)
            gameObject.SetActive(false);

        gameObject.SetActive(true);
    }
}