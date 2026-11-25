# Held items

The system uses one full-body rig, the right hand and one NetworkObject per
item. Separate first-person arms and two-handed poses are outside scope.
The server owns inventory state. Each client evaluates poses locally.

## Ownership

| Component | Responsibility |
| --- | --- |
| `Inventory` | Replicated slot snapshot and selected item. Configure `capacity` (code default 4) to match the inventory UI. |
| `ItemHolder` | Camera/shoulder poses, walking bob, action offsets and equip transition |
| `PlayerHeadSync` | Arm IK and authored finger curl after camera effects |
| `Item` | Aligning to the solved palm and hiding stored presentation |

The owner view adds a subtle walking bob from actual grounded movement, before
camera clearance and arm IK. Tune ItemHolder's `walkBobPosition`,
`walkBobRotation`, `walkBobStrideLength` and `walkBobBlendSpeed`, or disable
the effect with `enableWalkBob`. Bob settles at rest, in the air, when paused,
while interacting or when immobilized. Crouching and action poses reduce it.
`runBobFrequencyMultiplier` (code default 1.45) blends to a faster cadence
while preserving the authored amplitudes and current stride phase.

## Authoring

1. Set `Item.gripPoint` at the physical handle contact, with axes describing the
   grip coordinate system. Adjust framing with the pose offsets in step 3.
2. Calibrate `ItemHolder.palmPositionLocal` in hand-bone coordinates and
   `handGripRotationOffset` for grip-to-wrist orientation. Both views share this
   calibration, including skeleton scale compensation.
3. Set `viewPositionOffset` / `viewRotationOffset` for first person and
   `remotePositionOffset` / `remoteRotationOffset` for the world pose. These move
   the whole hand/item assembly.
4. Use `actionView*`, `actionRemote*` and `poseBlendDuration` for an alternate
   pose, with `IsHoldActionActive` supplying the action state.
5. For clips, create an identity child `HoldAnimationOffset`, assign
   `holdAnimationOffset` and animate that child's local transform with a dedicated
   Animator. Do not animate the network root, GripPoint or player IK target.
   The body locomotion Animator must not drive item actions.
6. Item automatically collects renderers, lights, canvases and audio sources
   from its hierarchy at initialization, including inactive children. List
   detached effects that should stay visible in `independentRenderers`.
   Assign the pickup and child physics colliders in `inventoryColliders`.
   Keep local Volume boundary colliders (such as
   Subwoofer's EffectArea trigger) out of `inventoryColliders` so held equipment
   retains its post-processing area.
7. Set `heldCameraBounds` to physical mesh bounds in item-root coordinates,
   excluding particles/effect volumes. Recompute after geometry changes.
8. Assign PlayerHeadSync's `rightFingerBase`, `rightFingerMiddle` and
   `rightFingerTip` to the three right-hand finger joints. Set each Item's
   `heldFingerCurl` to positive base/middle/tip angles to wrap the fingers around
   its handle. Zero preserves the animated pose. The current mitten rig curls
   around each joint's local Z axis. The additive pose is shared by both
   views and restored before animation, unequip and despawn.

The wrist is solved before the item locks to the palm, including the first
equip frame. Owner poses use a camera-relative grip with forward correction
when bounds enter `ItemHolder.cameraClearance` (code default 0.12 m). Local
shoulder extension preserves arm lengths and palm contact. The shoulder resets
before the next animation pass and on despawn. Remote reach is handled
separately. Terminal interaction retains its frozen world-space pose.

## Lifecycle

- Selection starts ItemHolder's procedural raise and wrist turn. In the
  Inspector, tune `equipDuration` (code default 0.32 s), `equipPositionOffset`
  and `equipRotationOffset`. Switching restarts the transition for the latest
  item. Selecting the same slot preserves the current transition. Empty slots
  release the arm. Items can be used during the animation.
- `AddItem` indicates whether it submitted a request. Display follows the
  server-acknowledged inventory state. See [Networking](NETWORKING.md).
- Stored roots remain active and parented to the player. Presentation hides
  graphics/audio and disables colliders/physics. Hidden items follow the holder
  for mission cleanup.
- Slot and parent messages may arrive in either order; reference resolution
  retries. Mission promotion can remove/reapply parents without losing slot IDs.
- Despawn removes its slot. Death drops all items on the server and turns them
  off. Normal drops preserve power. Changing slots turns the previous item off.
- An equipped powered radio raises while transmitting and lowers after the
  0.35-second packet grace period. Unequip, drop or power-off ends that pose.
  Voice activation uses the RMS threshold. Push-to-talk sends while recording.

## Verification

Check the real Player rig and item prefab in standing/crouch idle/crouch walk,
several animation phases, look pitches and equip weights. Include large items
and extreme angles. Confirm camera clearance, palm contact, preserved arm
lengths and restored shoulder pose.
Inspect first-person and remote views, including terminal entry/exit.

With a host and client, verify pickup, rapid/empty-slot selection, late-join slot
and radio state, parent/visibility order, rejection of foreign-player requests,
powered drops, death drops, despawn and mission return with stored/equipped items.
Test microphone capture and Steam transport with both voice input modes.
