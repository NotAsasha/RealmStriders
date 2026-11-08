# Held items

The system uses one full-body rig, the right hand and one NetworkObject per
item. Inventory state is server-owned; poses are evaluated locally.

## Ownership

| Component | Responsibility |
| --- | --- |
| `Inventory` | Replicated slot snapshot and selected item |
| `ItemHolder` | Camera/shoulder poses, action offsets and equip transition |
| `PlayerHeadSync` | Arm IK after camera effects |
| `Item` | Aligning to the solved palm and hiding stored presentation |

## Authoring

1. Set `Item.gripPoint` at the physical handle contact, with axes describing the
   grip coordinate system. Use pose offsets to adjust framing rather than moving
   the handle.
2. Calibrate `ItemHolder.palmPositionLocal` in hand-bone coordinates and
   `handGripRotationOffset` for grip-to-wrist orientation. Both views share this
   calibration, including skeleton scale compensation.
3. Set `viewPositionOffset` / `viewRotationOffset` for first person and
   `remotePositionOffset` / `remoteRotationOffset` for the world pose. These move
   the whole hand/item assembly.
4. Use `actionView*`, `actionRemote*` and `poseBlendDuration` for an alternate
   pose, with `IsHoldActionActive` supplying the action state. WalkieTalkie uses
   voice transmission with a 0.35-second release grace period between packets.
5. For clips, create an identity child `HoldAnimationOffset`, assign
   `holdAnimationOffset` and animate that child's local transform with a dedicated
   Animator. Do not animate the network root, GripPoint or player IK target.
   The body locomotion Animator must not drive item actions.
6. Explicitly assign `inventoryRenderers`, `inventoryLights`, `inventoryCanvases`,
   `inventoryAudioSources` and `inventoryColliders`, including particle renderers
   and child colliders. No runtime hierarchy searches supply missing references.
7. Set `heldCameraBounds` to physical mesh bounds in item-root coordinates,
   excluding particles/effect volumes. Recompute after geometry changes.

The wrist is solved before the item locks to the actual palm, including the
first equip frame. Owner poses use a camera-relative grip with forward correction
when bounds enter `ItemHolder.cameraClearance` (0.12 m). Local shoulder extension
preserves arm lengths and palm contact and is restored before the next animation
pass/on despawn. Remote reach remains separate. Terminal interaction retains its
frozen world-space pose. Extreme angles/large items still need visual review.

## Lifecycle

- Selection starts ItemHolder's procedural raise/wrist turn. Tune `equipDuration`
  (0.32 s), `equipPositionOffset` and `equipRotationOffset` in the Inspector.
  Rapid switching restarts for the latest item; selecting the same slot does not.
  Empty slots release the arm. Animation does not delay use or add RPCs.
- `AddItem` returns request submission, not server acceptance. Display follows
  acknowledged inventory state; see [Networking](NETWORKING.md).
- Stored roots remain active and parented to the player. Presentation hides
  graphics/audio and disables colliders/physics. Hidden items follow the holder
  for mission cleanup.
- Slot and parent messages may arrive in either order; reference resolution
  retries. Mission promotion can remove/reapply parents without losing slot IDs.
- Despawn removes its slot. Death drops all items on the server and turns them
  off. Normal drops preserve powered devices; changing slots turns them off.
- An equipped powered radio raises while transmitting and lowers after the
  packet grace period. Unequip, drop or power-off ends that pose. Voice activation
  uses the existing RMS threshold; push-to-talk sends while recording.

## Verification

Check the real Player rig and item prefab in standing/crouch idle/crouch walk,
several animation phases, look pitches and equip weights. Confirm camera
clearance, palm contact, unchanged arm lengths and restored shoulder pose.
Inspect first-person and remote views, including terminal entry/exit.

With a host and client, verify pickup, rapid/empty-slot selection, late-join slot
and radio state, parent/visibility order, rejection of foreign-player requests,
powered drops, death drops, despawn and mission return with stored/equipped items.
Test Steam voice in both input modes separately; local loopback does not verify
microphone capture or Steam transport.
