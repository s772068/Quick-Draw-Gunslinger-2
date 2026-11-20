using UnityEngine;

public sealed class MenuUiSkin : ScriptableObject
{
    [SerializeField] private Sprite button;
    [SerializeField] private Sprite pressedButton;
    [SerializeField] private Sprite panel;

    public Sprite Button => button;
    public Sprite PressedButton => pressedButton;
    public Sprite Panel => panel;

    public static MenuUiSkin Load() => Resources.Load<MenuUiSkin>("MenuUiSkin");

    public static bool NeedsDefaultArt(Sprite sprite)
    {
        return sprite == null || sprite.name == "UISprite" || sprite.name == "Background" || sprite.name == "Knob";
    }
}
