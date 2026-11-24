# PickleVerse VFX

Home of the game's visual effects. Each effect family has its own folder; shared shaders live in `Shaders/`.

```
VFX/
  Shaders/                 additive URP shaders shared by every effect
    SkinTrail.shader       ball trail ribbon (TrailRenderer): gradient, sliding facets, twinkles
    AdditiveFlipbook.shader  particle sprites / flipbooks (glints, dust, flashes)
    BallGlitter.shader     faceted glitter shell that sparkles around the ball
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
| `paddle.gemstone` | Royal Court Sparkle | gold-to-emerald ribbon with diamond star glints, golden gem dust, glints orbiting a glittering ball, star burst on contact |

## Adding a new effect

1. Make its textures in Blender with a script under `ArtSource/VFX/<Skin>/` (see `ArtSource/VFX/Gemstone/build_gemstone_vfx.py`;
   `preview` mode renders a design preview, `flipbook` mode writes the textures here).
2. Add a `Build<Skin>()` method to `Scripts/VFX/Editor/PaddleSkinVfxBuilder.cs` and run
   **PickleVerse > VFX > Rebuild paddle skin effects**. It creates the materials, prefabs and profile and registers the
   profile in the library.
3. Keep particle systems in world space, without shadows or light probes, and under ~150 particles per effect.
4. Add the skin to the table above and a case to `src/Testing/Unity/EditMode/VFX`.
