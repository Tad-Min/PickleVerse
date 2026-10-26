# Natural park assets — 26/10/2026

Open `NaturalVegetation.blend` to inspect both broadleaf trees, the shrub and the
grass patch at three detail levels. The complete `ProfessionalStadium.blend` and
`PickleballCourt_LowPoly.blend` also contain the new park as linked mesh copies.
Textures are packed. Play frames 1–180 for the looping wind preview.

Authoring units are metres, Blender Z-up. glTF exports Y-up. Trees originate at
the trunk base, grass patches at ground level. Unity's source importer performs
the remaining axis conversion. The layout JSON records Unity positions.

## Runtime assets

- `Assets/EnvironmentContent/NaturalPark/NaturalVegetation.glb`: shared mesh
  prototypes, all three LODs, UVs and 8-bit normalized wind/occlusion colours.
- `Assets/EnvironmentContent/NaturalPark/NaturalFoliage.shader`: opaque,
  double-sided botanical surfaces, GPU wind, fog, main-light shadows and
  additional stadium lighting. No transparent rectangular leaf cards.
- `Assets/Resources/Courts/Professional.prefab` and `LowPoly.prefab`: core venue
  visual plus a `Natural Park` hierarchy of shared mesh/material instances.
- Shared bark, leaf, grass and organic-ground maps: 512px maximum for bark/leaf,
  256px for grass/ground, mipmaps and ASTC 6x6 mobile overrides.

Vertex colour R is bending weight, G phase, B occlusion and A tonal variation.
Grass roots have zero bending. The shader bends leaves/blade tips continuously;
the GLB does not carry duplicate animated vertex streams or an Animator.
Shadow and depth passes use the same displacement as the visible pass.

Every plant has a LODGroup. Grass uses full geometry only near the camera,
then thins and disappears with distance. Existing mobile quality settings
automatically skip LOD0. Vegetation is not marked static for batching; meshes
and instanced materials are shared. Grass casts no shadow and has no colliders.

## Rebuild and inspect

1. Run `natural_park_textures.py` with Python, Pillow and NumPy.
2. Run Blender 5.1 with `--background --python build_natural_park.py`.
   It preserves court/net geometry and replaces only the generated garden.
   It uses the task backup if present, otherwise the complete source blends.
3. In Unity Edit mode run `NaturalParkInstaller.Install()` through the existing
   Editor tooling. Let imports/compilation finish before invoking it.
4. Run `NaturalParkInstaller.Validate()` and the geometry/scene validators.
   Run `NaturalParkRuntimeChecks.Profile()` and `WindAndCloseup()` separately in
   an isolated Play session. These are Editor-only local fixtures.

The `.blend` files and these scripts live outside `Assets`, so Unity does not
ship or automatically convert the authoring files. Runtime GLBs have no embedded
duplicate botanical textures. All botanical geometry and maps were generated
in this project; no downloaded vegetation package is required.
