# Ruined arena blockout

The playable floor is a 40 x 34 m oval with a MeshCollider. Six concentric
stone grandstand tiers rise from 2.4 m at the playable boundary to 10.9 m at
the outside, with an outer screen reaching about 13.5 m. Only the inner tier
has collision; the remaining tiers are scenery outside the playable area.
Four low covers have colliders. Four pillars are visual placeholders for the
planned destructible objects; they currently have no collision or destruction
behavior. Ring stones and broken caps are decorative only.

Apply or regenerate the layout through **Chainbound Raid > Apply Ruined Arena
Blockout**. The script is `Assets/Editor/PrototypeArenaSetup.cs`. It rebuilds
only the arena objects and materials, leaving boss, players, and chain logic in
place. The chain does not wrap around terrain or pillars.

This is a gameplay blockout. Final stone meshes, textures, lighting, debris,
destruction behavior, and camera clearance still need art and play review.
