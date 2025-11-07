using System;
using System.Collections.Generic;
using Landfall.Haste;
using UnityEngine;
using UnityEngine.UI;

namespace AethaModelSwapMod;

public class ModelParamsEditor : MonoBehaviour
{
    private const string PrefabName = "ModelEditorUI";
    const string GizmoName = "GizmoPrefab";
    private const string InputFieldFormat = "0.00";
    private const float InputFieldRounding = 100f;
    
    private static ModelParamsEditor _instance;
    static ModelParamsEditor Instance
    {
        get
        {
            if (!_instance)
            {
                InitializeEditorUI();
                if (!_instance)
                {
                    Debug.LogError("ModelParamsEditor instance is not ready");
                }
            }
            return _instance;
        }
    }
    
    public static bool IsEditorOpen => Instance && Instance.gameObject.activeSelf;
    
    private static readonly List<(Slider slider, InputField inputField)> EditorSliders = new();
    private static readonly List<Toggle> EditorToggles = new();
    private static readonly Dictionary<string, Transform> Gizmos = new();

    private static Text _modelNameLabel;
    private static Image _modelIconImage;
    
    private static GameObject _sliderPrefab;
    private static GameObject _togglePrefab;
    private static GameObject _separatorPrefab;
    private static GameObject _gizmoPrefab;
    
    private static ModelIKParameters EditorParams => AethaModelSwap.LocalClone ? AethaModelSwap.LocalClone.modelIKParameters : new ModelIKParameters();

    static void InitializeEditorUI()
    {
        var bundle = AssetBundle.LoadFromFile(AethaModelSwap.EditorAssetBundlePath());
        if (bundle == null)
        {
            Debug.Log($"Failed to load bundle {AethaModelSwap.EditorAssetBundlePath()}");
            return;
        }
        var editorPrefab = bundle.LoadAsset<GameObject>(PrefabName);
        if (!editorPrefab)
        {
            Debug.Log($"No prefab for model editor ui with name: {PrefabName}");
            return;
        }
        _gizmoPrefab = bundle.LoadAsset<GameObject>(GizmoName);
        
        var newObj = Instantiate(editorPrefab);
        _instance = newObj.AddComponent<ModelParamsEditor>();
        newObj.SetActive(false);
        CursorHandler.RegisterCursorNeeder(newObj);
    }

    private void Awake()
    {
        foreach (var t in gameObject.GetComponentsInChildren<RectTransform>())
        {
            switch (t.name)
            {
                case "SliderPrefab":
                    _sliderPrefab = t.gameObject;
                    break;
                case "TogglePrefab":
                    _togglePrefab = t.gameObject;
                    break;
                case "SeparatorPrefab":
                    _separatorPrefab = t.gameObject;
                    break;
                case "ModelLabel":
                    _modelNameLabel = t.GetComponent<Text>();
                    break;
                case "ModelIcon":
                    _modelIconImage = t.GetComponent<Image>();
                    break;
                case "NextButton":
                    t.GetComponent<Button>().onClick.AddListener(AethaModelSwap.NextSkin);
                    break;
                case "PreviousButton":
                    t.GetComponent<Button>().onClick.AddListener(AethaModelSwap.PreviousSkin);
                    break;
                case "ResetButton":
                    t.GetComponent<Button>().onClick.AddListener(Reset);
                    break;
                case "SaveButton":
                    t.GetComponent<Button>().onClick.AddListener(Save);
                    break;
                case "ExitButton":
                    t.GetComponent<Button>().onClick.AddListener(ExitEditor);
                    break;
            }
        }
        _sliderPrefab.gameObject.SetActive(false);
        _togglePrefab.gameObject.SetActive(false);
        _separatorPrefab.gameObject.SetActive(false);
        DontDestroyOnLoad(gameObject);
        gameObject.SetActive(false);
        CursorHandler.RegisterCursorNeeder(gameObject);
        
        AddSliderField("Scale", 1f, 0.5f, 4f, x => EditorParams.scale = x);
        AddSliderField("Vertical Offset", 0f, -1f, 1f, x => EditorParams.verticalOffset = x);
        AddSeparator();
        AddSliderField("Stance Width", 1f, 0f, 4f, x => EditorParams.stanceWidth = x);
        AddSliderField("Stance Height", 0.9f, 0f, 1f, x => EditorParams.stanceHeight = x);
        AddSliderField("Feet Forward", 0f, -0.5f, 0.5f, x => EditorParams.footFrontBackOffset = x);
        AddSliderField("Stride Length", 1f, 0f, 1f, x => EditorParams.strideLength = x);
        AddSeparator();
        AddSliderField("Knee Angle", 0f, 0f, 1f, x => EditorParams.kneesOut = x);
        AddSliderField("Foot Angle", 0f, -45f, 45f, x => EditorParams.footAngle = x);
        AddSliderField("Head Angle", 0f, -45f, 45f, x => EditorParams.headAngleOffset = x);
        AddSliderField("Arm Angle", 0f, -45f, 45f, x => EditorParams.armAngleOffset = x);
        AddSliderField("Hand Angle", 0f, -45f, 45f, x => EditorParams.handAngleOffset = x);
        AddSeparator();
        AddToggleField("Replace Standard materials", false, x =>
        {
            EditorParams.replaceStandardShader = x;
            if (AethaModelSwap.LocalClone)
            {
                if (EditorParams.replaceStandardShader)
                {
                    AethaModelSwap.LocalClone.SetMaterials(false);
                }
                else
                {
                    AethaModelSwap.SetSkin(AethaModelSwap.LocalClone.SkinIndex);
                    EditorParams.replaceStandardShader = false;
                }
            }
        });
        
        ResetFields();
    }

    void Reset()
    {
        if (!AethaModelSwap.LocalClone || string.IsNullOrEmpty(AethaModelSwap.LocalClone.modelIKParameters.savePath)) return;
        AethaModelSwap.LocalClone.modelIKParameters = ModelIKParameters.LoadModelIKParameters(AethaModelSwap.LocalClone.modelIKParameters.savePath);
        ResetFields();
    }

    public static void ResetFields()
    {
        if (!_instance)
        {
            return;
        }
        Debug.Log($"Fields reset: {AethaModelSwap.LocalClone}");
        if (AethaModelSwap.LocalClone)
        {
            _modelNameLabel.text = AethaModelSwap.LocalClone.name;
            var sprite = AethaModelSwap.GetSprite(AethaModelSwap.LocalClone.SkinIndex);
            if (sprite)
            {
                _modelIconImage.sprite = sprite;
                _modelIconImage.gameObject.SetActive(true);
            }
            else
            {
                _modelIconImage.gameObject.SetActive(false);
            }
        }
        else
        {
            _modelNameLabel.text = "Default Model";
            _modelIconImage.sprite = SkinDatabase.me.GetSkin(0).PlayerIcon;
            _modelIconImage.gameObject.SetActive(true);
        }

        SetFloats(EditorParams.scale,
            EditorParams.verticalOffset,
            
            EditorParams.stanceWidth,
            EditorParams.stanceHeight,
            EditorParams.footFrontBackOffset,
            EditorParams.strideLength,
            
            EditorParams.kneesOut,
            EditorParams.footAngle,
            EditorParams.headAngleOffset,
            EditorParams.armAngleOffset,
            EditorParams.handAngleOffset);
        SetBools(EditorParams.replaceStandardShader);
    }

    void Save()
    {
        if (!AethaModelSwap.LocalClone || string.IsNullOrEmpty(AethaModelSwap.LocalClone.modelIKParameters.savePath)) return;
        ModelIKParameters.SaveModelIKParameters(AethaModelSwap.LocalClone.modelIKParameters.savePath, AethaModelSwap.LocalClone.modelIKParameters);
    }
    
    public static void OpenEditor()
    {
        ResetFields();
        Instance.gameObject.SetActive(true);
    }

    public static void ExitEditor()
    {
        Instance.gameObject.SetActive(false);
        foreach (var gizmo in Gizmos.Values)
        {
            gizmo.gameObject.SetActive(false);
        }
    }

    // Display a transform gizmo at this position, with axis lengths of scale
    // These are not automatically cleaned up, but they are cached so if you use the same labels consistently there won't be a problem
    public static void SetGizmo(string label, Vector3 position, Quaternion rotation, float scale = 0.2f)
    {
        if (!Gizmos.TryGetValue(label, out var gizmo))
        {
            Gizmos[label] = Instantiate(_gizmoPrefab, parent: null).transform;
            gizmo = Gizmos[label];
        }
        gizmo.position = position;
        gizmo.rotation = rotation;
        gizmo.localScale = Vector3.one * scale;
        gizmo.gameObject.SetActive(IsEditorOpen);
    }

    static void SetFloats(params float[] values)
    {
        for (int i = 0; i < EditorSliders.Count; i++)
        {
            if (i >= values.Length)
            {
                return;
            }
            var uiElement = EditorSliders[i];
            var value = values[i];
            uiElement.slider.SetValueWithoutNotify(value);
            uiElement.inputField.SetTextWithoutNotify(value.ToString(InputFieldFormat));
        }
    }

    static void SetBools(params bool[] values)
    {
        for (int i = 0; i < EditorToggles.Count; i++)
        {
            if (i >= values.Length)
            {
                return;
            }
            EditorToggles[i].SetIsOnWithoutNotify(values[i]);
        }
    }

    static void AddSliderField(string label,
        float defaultValue,
        float minValue,
        float maxValue,
        Action<float> onValueChanged)
    {
        var newObj = Instantiate(_sliderPrefab, parent: _sliderPrefab.transform.parent);
        var text = newObj.GetComponentInChildren<Text>(true);
        var slider = newObj.GetComponentInChildren<Slider>(true);
        var inputField = newObj.GetComponentInChildren<InputField>(true);
        text.text = label;
        slider.SetValueWithoutNotify(defaultValue);
        inputField.SetTextWithoutNotify(defaultValue.ToString(InputFieldFormat));
        slider.minValue = minValue;
        slider.maxValue = maxValue;
        slider.onValueChanged.AddListener(ChangeFloat);
        inputField.onValueChanged.AddListener(ChangeString);
        newObj.SetActive(true);
        
        EditorSliders.Add((slider, inputField));

        void ChangeFloat(float value)
        {
            value = Mathf.Round(value * InputFieldRounding) / InputFieldRounding;
            inputField.SetTextWithoutNotify(value.ToString(InputFieldFormat));
            onValueChanged?.Invoke(value);
        }
        void ChangeString(string value)
        {
            if (!float.TryParse(value, out var floatValue)) return;
            floatValue = Mathf.Round(floatValue * 100f) / 100f;
            slider.SetValueWithoutNotify(floatValue);
            onValueChanged?.Invoke(floatValue);
        }
    }

    static void AddToggleField(string label,
        bool defaultValue,
        Action<bool> onValueChanged)
    {
        var newObj = Instantiate(_togglePrefab, parent: _togglePrefab.transform.parent);
        var text = newObj.GetComponentInChildren<Text>(true);
        var toggle = newObj.GetComponentInChildren<Toggle>(true);
        text.text = label;
        toggle.SetIsOnWithoutNotify(defaultValue);
        toggle.onValueChanged.AddListener(ChangeBool);
        newObj.SetActive(true);
        
        EditorToggles.Add(toggle);
        
        void ChangeBool(bool value)
        {
            onValueChanged?.Invoke(value);
        }
    }

    static void AddSeparator()
    {
        Instantiate(_separatorPrefab, parent: _separatorPrefab.transform.parent).SetActive(true);
    }
    
}
