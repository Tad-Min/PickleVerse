# PickleVerse VFX

Home of the game's visual effects. Each effect family has its own folder; shared shaders live in `Shaders/`.

```
VFX/
  Shaders/                 additive URP shaders shared by every effect
    SkinTrail.shader       ball trail ribbon (TrailRenderer): gradient, sliding facets, twinkles
    AdditiveFlipbook.shader  particle sprites / flipbooks (glints, dust, flashes)
    BallGlitter.shader     faceted glitter shell that sparkles around the ball
    DragonSpirit.shader    bends the straight dragon mesh along the sine flight (DragonSpine.hlsl holds the shared maths)
    DragonRibbon.shader    burning ribbon laid on the same sine curve
    DragonFire.shader      premultiplied fire sprites (particles, trail): red fire stays red over the blue court; _HotColor sets the core
    PhoenixSpirit.shader   the blue phoenix: rigid body and flapping wings on the flight, tail bent along the path, tips burning to ash
    BlackAsh.shader        premultiplied black ash and black flames whose embers glow in the particle colour
  PaddleSkins/<Skin>/      signature ball effect of one paddle skin
    Textures/              made in Blender (source: ArtSource/VFX/<Skin>/)
    Materials/  Prefabs/   built by the editor tool below
    <Skin>.asset           PaddleSkinVfxProfile (skin id, trail look, ball and contact effects)
Resources/VFX/PaddleSkinVfxLibrary.asset   every profile the game loads at runtime
```

## How a paddle-skin effect works

- Every ball (bot match, online, custom room, training, tutorial) is registered through
  `BallPresentationRegistry.Register`, and every successful paddle contact is reported from `MatchActor.ConsumeContact`.
- `BallSkinVfx` looks up the hitter's rendered paddle skin (`PaddleAppearance.CurrentId`, already replicated online) in the
  library. A skin with a profile gets its trail, ball sparkle and contact burst; any other paddle restores the standard
  trail on its next hit. Presentation only: no network data, rules, physics or replays change.

## Paddle skins

| Skin id | Effect | Look |
| --- | --- | --- |
| `paddle.dragonruby` | Xích Long Truy Châu | a red dragon leaves the paddle and chases the glowing ball on a sine wave, with a fire ribbon, a mirrored golden ribbon and a dragon-head roar on contact |
| `paddle.gemstone` | Royal Court Sparkle | gold-to-emerald ribbon with diamond star glints, golden gem dust, glints orbiting a glittering ball, star burst on contact |
| `paddle.bluephoenix` | Lam Phượng Tung Cánh | a glowing blue phoenix bursts out of the paddle, spreads and beats its wings after the ball; its tail trails a blue fire ribbon and a black smoke ribbon, feather tips burn to black ash with blue embers, ash and black flames fall behind; a blue fire ring and the phoenix emblem flash on contact |

An effect that moves by itself derives from `BallSkinMotion` on its ball prefab: `BallSkinVfx` calls `Restart` on every
contact by that skin and `SetShown` every frame. `SpineBallMotion` (base of `DragonBallMotion` and `PhoenixBallMotion`)
records the ball's rendered path and hands 32 points and the wave to the shaders (y = A sin(2π x / wavelength), x = distance
flown since the contact).

Dragon Ruby sources: `ArtSource/VFX/DragonRuby` (`sculpt_dragon.py`, `make_vfx_textures.py`, `build_dragon_vfx.py`); the paddle
itself is `ArtSource/Paddles/DragonRuby`, assembled by **PickleVerse > Shop > Rebuild Dragon Ruby paddle**.

Blue Phoenix sources: `ArtSource/VFX/BluePhoenix/build_phoenix_vfx.py` (sprites, the phoenix mesh, its emblem); the paddle is
`ArtSource/Paddles/BluePhoenix` (`make_phoenix_maps.py` turns the reference painting into colour, relief, normal and mask
maps, `build_blue_phoenix.py` models, renders and exports it). Both run inside Blender, numpy only. **PickleVerse > Shop >
Rebuild Blue Phoenix paddle and effect** assembles both in Unity; **PickleVerse > VFX > Film paddle skin effect** films an
effect in the BotMatch court for review.

## Adding a new effect

1. Make its textures in Blender with a script under `ArtSource/VFX/<Skin>/` (see `ArtSource/VFX/Gemstone/build_gemstone_vfx.py`;
   `preview` mode renders a design preview, `flipbook` mode writes the textures here).
2. Add a `Build<Skin>()` method to `Scripts/VFX/Editor/PaddleSkinVfxBuilder.cs` and run
   **PickleVerse > VFX > Rebuild paddle skin effects**. It creates the materials, prefabs and profile and registers the
   profile in the library.
3. Keep particle systems in world space, without shadows or light probes, and under ~150 particles per effect.
4. Add the skin to the table above and a case to `src/Testing/Unity/EditMode/VFX`.
