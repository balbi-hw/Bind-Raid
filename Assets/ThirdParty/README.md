# Temporary character art

- Players: Kenney Blocky Characters 2.0, CC0. Only variants A and B and their
  textures are kept from the original download. See
  `KenneyBlockyCharacters/License.txt` and
  https://opengameart.org/content/blocky-characters.
- Current boss: user supplied `fantasy mammoth 3d model.glb`, converted to a
  decimated FBX with its base color and normal textures for Unity. See
  `MammothPrototype/README.md`.
- Previous boss placeholder: syncopika's low poly elephant, CC0. See
  `ElephantPrototype/LICENSE.txt` and
  https://opengameart.org/content/low-poly-elephant-model.

The visible models and their animations are children of the existing player
prefab and boss scene object. Gameplay, colliders, chain anchors, status rings,
and network positions remain on the original roots so final art can replace
these temporary children without changing gameplay code.
