# Tetra Rush redesign — Unity import guide

## Files in this pack
- `ui_panel_glass.png` — translucent rounded card, use as-is (white) for menu buttons, the board frame, and stat cards.
- `ui_glow_tile.png` — plain white rounded square with a soft glow baked into the alpha. **Tint this with any accent color** to get the cyan/coral/gold/magenta/green buttons and blocks from the mockup — one sprite, five looks.
- `ui_pill.png` — same idea, stadium-shaped, for the currency pill and stat chips.
- `TetraRushPalette.cs` — every hex value from the mockup as `Color32` constants, drop it in `Assets/Scripts`.

## Importing the sprites
1. Drag the PNGs into `Assets/UI/Sprites`.
2. Select each one → Inspector → **Texture Type: Sprite (2D and UI)** → **Mesh Type: Full Rect** → **Sprite Mode: Single**.
3. Click **Sprite Editor** and set the 9-slice border so corners don't stretch:
   - `ui_panel_glass.png` (512×512): border ≈ **80, 80, 80, 80**
   - `ui_glow_tile.png` (464×464): border ≈ **100, 100, 100, 100**
   - `ui_pill.png` (572×240): border ≈ **120, 30, 120, 30** (left, top, right, bottom)
4. On the `Image` component using these sprites, set **Image Type: Sliced**, and just tint via `Color` — no shader needed. `ui_panel_glass` should generally stay white/untinted since its translucency is baked in.

## Fonts
- Display font: **Baloo 2** (weight 700/800) — Google Fonts, free.
- Body/UI font: **Inter** (weight 400–600) — Google Fonts, free.
- Download the `.ttf` files, import into Unity, then right-click → **Create → TextMeshPro → Font Asset** to generate the TMP font asset (use the Font Asset Creator with a high enough atlas resolution, e.g. 1024×1024, if you need larger display text).

## Quick mapping to the mockup
- Menu button → `Image` with `ui_panel_glass`, an icon `Image` tinted per-mode (cyan/gold), `TMP_Text` title in Baloo 2 + subtitle in Inter.
- Classic CTA button → `Image` with `ui_glow_tile` tinted `TetraRushPalette.Coral`, white text on top.
- Board cell → `Image` with `ui_glow_tile` tinted per block color when filled, or `TetraRushPalette.BorderSoft`-tinted panel when empty.
- Level tile → same `ui_glow_tile`, tinted `Cyan` (done), `Gold` + `transform.localScale = 1.06` (current), or grey/low-alpha (locked).
