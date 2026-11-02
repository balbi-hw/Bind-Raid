# Temporary character art

- Current players: the supplied Spartan Tripo model in `SpartanTripo/`, with
  separate textures and a rigged FBX. See `docs/TRIPO_REPLACEMENT_REVIEW.md`.
  Cloth tint distinguishes the two player slots.
- Earlier prototype: the project owner-created ancient warrior in
  `AncientWarriorPrototype/`. See its README for source and permission details.
- Previous player placeholders: Kenney Blocky Characters 2.0, CC0. Their
  imported variants remain in the project as fallback. See
  `KenneyBlockyCharacters/License.txt` and
  https://opengameart.org/content/blocky-characters.
- Current boss: project owner-created `fantasy mammoth 3d model.glb`, converted to a
  decimated FBX with its base color and normal textures for Unity. See
  `MammothPrototype/README.md`.
- Previous boss placeholder: syncopika's low poly elephant, CC0. See
  `ElephantPrototype/LICENSE.txt` and
  https://opengameart.org/content/low-poly-elephant-model.

The visible models and their animations are children of the existing player
prefab and boss scene object. Gameplay, colliders, chain anchors, status rings,
and network positions remain on the original roots so final art can replace
these temporary children without changing gameplay code.
