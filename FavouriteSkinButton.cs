using Landfall.Haste;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zorro.ControllerSupport;

namespace AethaModelSwapMod;

public class FavouriteSkinButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public SkinManager.Skin skin;
    
    private static readonly Color SelectedColorOuter = new (0f, 0f, 0f, 0.5f);
    private static readonly Color SelectedColorInner = new (0.9226f, 0.5203f, 0.9890f, 1f);
    private static readonly Color BaseColorOuter = new(0f, 0f, 0f, 0.0f);
    private static readonly Color BaseColorInner = new(0f, 0f, 0f, 0.5f);

    private const float HoverAnimationTime = 0.05f;
    private const float SelectedAnimationTime = 0.1f;

    private bool _hovered;
    private float _hoveredBlend;
    private bool _selected;
    private float _selectedBlend;
    
    private Image _outerImage;
    private Image _innerImage;
    
    private void Start()
    {
        var button = GetComponentInChildren<Button>();
        if (!button)
        {
            Debug.LogError("No button on star prefab");
            Destroy(gameObject);
            return;
        }
        button.onClick.AddListener(Toggle);
        foreach (var image in gameObject.GetComponentsInChildren<Image>())
        {
            switch (image.name)
            {
                case "Star":
                    _outerImage = image;
                    break;
                case "InnerStar":
                    _innerImage = image;
                    break;
            }
        }
        var parentButton = transform.parent.GetComponent<Button>();
        parentButton.onClick.AddListener(() =>
        {
            if (GeneralPatches.selectedHead == skin && GeneralPatches.selectedHead == GeneralPatches.prevSelectedHead && InputHandler.GetCurrentUsedInputScheme() == InputScheme.Gamepad)
            {
                Toggle();
            }
        });
        _selected = IsSkinFavourite(skin);
    }

    private void Update()
    {

        _selectedBlend += (_selected ? Time.unscaledDeltaTime : -Time.unscaledDeltaTime) / SelectedAnimationTime;
        _selectedBlend = Mathf.Clamp01(_selectedBlend);
        _hoveredBlend += (_hovered ? Time.unscaledDeltaTime : -Time.unscaledDeltaTime) / HoverAnimationTime;
        _hoveredBlend = Mathf.Clamp01(_hoveredBlend);

        transform.localScale = Vector3.one * (0.35f + _hoveredBlend * 0.05f);
        _outerImage.color = Color.Lerp(BaseColorOuter, SelectedColorOuter, Mathf.SmoothStep(0f, 1f, _selectedBlend));
        _innerImage.color = Color.Lerp(BaseColorInner, SelectedColorInner, Mathf.SmoothStep(0f, 1f, _selectedBlend));
    }

    private static Fact GetFact(int index) => new ($"FavouriteSkin_{index}");
    public static bool IsSkinFavourite(SkinManager.Skin skin) => FactSystem.GetFact(GetFact((int)skin)) != 0f;
    public static void SetSkinFavourite(SkinManager.Skin skin, bool value) => FactSystem.SetFact(GetFact((int)skin), value ? 1f : 0f);


    private void Toggle()
    {
        var favourite = IsSkinFavourite(skin);
        SetSkinFavourite(skin, !favourite);
        _selected = !favourite;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = true;
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
    }
}