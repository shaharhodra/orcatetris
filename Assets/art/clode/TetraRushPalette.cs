using UnityEngine;

// Drop this script anywhere in Assets/Scripts.
// Usage: someImage.color = TetraRushPalette.Cyan;
public static class TetraRushPalette
{
    // Background gradient (use on a full-screen Image with a simple vertical/radial gradient shader,
    // or fake it with 3 stacked Images at these stops)
    public static readonly Color32 BgDeep = new Color32(0x15, 0x0A, 0x35, 0xFF);
    public static readonly Color32 BgMid  = new Color32(0x24, 0x14, 0x54, 0xFF);
    public static readonly Color32 BgTop  = new Color32(0x34, 0x1B, 0x7A, 0xFF);

    // Glass surfaces (use with ui_panel_glass.png, tint = white, alpha already baked in)
    public static readonly Color32 BorderSoft = new Color32(0xFF, 0xFF, 0xFF, 0x1F);

    // Accent colors — tint ui_glow_tile.png / ui_pill.png with these
    public static readonly Color32 Cyan       = new Color32(0x43, 0xE8, 0xFF, 0xFF);
    public static readonly Color32 CyanDeep   = new Color32(0x1F, 0xB8, 0xD4, 0xFF);
    public static readonly Color32 Coral      = new Color32(0xFF, 0x7A, 0x45, 0xFF);
    public static readonly Color32 CoralDeep  = new Color32(0xE8, 0x55, 0x1D, 0xFF);
    public static readonly Color32 Gold       = new Color32(0xFF, 0xCD, 0x3C, 0xFF);
    public static readonly Color32 Magenta    = new Color32(0xFF, 0x5D, 0x9E, 0xFF);
    public static readonly Color32 Green      = new Color32(0x5E, 0xE6, 0xA0, 0xFF);

    // Text
    public static readonly Color32 TextHi  = new Color32(0xF5, 0xF2, 0xFF, 0xFF);
    public static readonly Color32 TextMid = new Color32(0xB6, 0xA9, 0xE0, 0xFF);
    public static readonly Color32 TextLow = new Color32(0x7C, 0x6F, 0xA8, 0xFF);

    // Block/piece colors, cycle through these when spawning random pieces
    public static readonly Color32[] BlockColors = { Cyan, Coral, Gold, Magenta, Green };
}
