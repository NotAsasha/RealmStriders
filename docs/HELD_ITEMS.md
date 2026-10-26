# Held items

The system keeps one full-body rig and one network object per item. Only the
right hand is used. Inventory state is authoritative on the server; poses are
evaluated locally.

## Authoring

1. Set `Item.gripPoint` on the physical contact point of the handle. Its axes
   describe the grip's coordinate system. Do not move it to fix screen framing.
2. `ItemHolder.palmPositionLocal` locates the palm in hand-bone coordinates.
   `handGripRotationOffset` converts grip orientation to wrist orientation. Both
   apply to owner and remote views. The wrist target compensates for the palm
   offset and skeleton scale before reach clamping.
3. Adjust the item's `viewPositionOffset` / `viewRotationOffset` for first-person
   composition, and `remotePositionOffset` / `remoteRotationOffset` for the world
   pose. These offsets change the target of the whole hand-and-item assembly.
4. `actionView*` / `actionRemote*` and `poseBlendDuration` describe an alternate
   held pose. Override `IsHoldActionActive` for an equipment action. WalkieTalkie
   uses validated voice transmission, with a 0.35-second release grace period
   to avoid flicker between packets. Push-to-talk sends packets while recording;
   voice activation sends packets above the existing RMS threshold.
5. For authored clips, create an identity child `HoldAnimationOffset` in the item
   prefab, assign it to `holdAnimationOffset`, and animate that child's local
   position/rotation with a dedicated Animator. It is an additive pose input;
   ItemHolder does not write to it. Do not animate the network root, GripPoint or
   the player's IK target directly. The body's locomotion Animator changes its
   speed with movement, so it should not drive item actions.
6. Assign `inventoryRenderers`, `inventoryLights`, `inventoryCanvases`,
   `inventoryAudioSources`, and `inventoryColliders` explicitly. Include particle
   renderers and all child colliders. Runtime code never searches for these.

PlayerHeadSync evaluates after camera effects, then solves the wrist, then locks
the item to the actual palm. The final attachment follows the solved hand even
during the equip-weight transition. The owner and remote views may have different
screen/world poses; neither requires streaming item transforms while held.

Full-body arm reach remains a physical constraint. Very large items and extreme
up/down camera angles can still extend outside the frame. Use per-item pose
offsets for composition rather than displacing the handle from the hand.

## Inventory lifecycle

- `AddItem` requests pickup; its return value means a request was submitted,
  not that the server accepted it. Presentation follows the acknowledged state.
- Slot changes and pickup/drop requests are validated on the server. The slot
  list and selected slot are durable state, so no replay of old RPCs is needed.
- Spawned roots stay active in stored slots. Graphics and audio are hidden,
  colliders are disabled, and the network parent remains the player.
- Both the slot list and item parent callbacks may arrive first. Resolution is
  retried until referenced items and their parents exist locally.
- Item despawn removes its slot; death drops all slots on the server. Normal
  drops preserve powered devices; slot changes and death turn them off.
- Stored items still update their world location with the player. Mission item
  promotion can temporarily remove/reapply parenting without losing slot IDs.

## Repeatable checks via Unity MCP

Use `run_script` with `tools/HeldItemChecks.cs`:

- `ValidatePrefabs(string[] paths)` checks presentation arrays and Player holder
  references for the supplied item prefab paths.
- `CheckGripAndPresentation()` checks exact grip alignment at multiple scales
  and verifies that hiding/restoring presentation keeps the network root active.
- `PreviewRig(itemPath, imagePath, firstPerson)` solves the real Player arm in
  a temporary preview scene, checks palm contact and renders a preview. Lights
  and particles on the copied item are disabled to inspect geometry clearly.

For a network integration check, use `tools/HeldItemNetworkChecks.cs`:

1. Run `Prepare` in Edit Mode. It creates temporary assets under
   `Assets/Temporary/HeldItemNetworkChecks` and assigns an isolated Play Mode
   start scene, leaving existing unsaved scenes untouched.
2. Enter Play Mode using Unity MCP, then run `Run` (timeout 55 seconds).
3. Exit Play Mode and run `Restore`, including after a failed check.

The integration check uses the production Inventory/Item RPCs with a local host
and two UDP clients in one Editor process. It checks late-join slots, parenting,
visibility, an ongoing radio transmission snapshot, powered LeafBlower/Subwoofer
effects, rejection of foreign player
requests, empty-slot selection, death-drop logic, client pickup/drop, powered
radio drops and despawn cleanup. It does not test Steam voice capture or Steam
transport, nor does it simulate the full mission scene transition.

Manual follow-up: Steam host/client voice in both input modes; walking/crouching
and extreme look angles; terminals; drop/unequip during transmission; mission
return and cleanup with stored and equipped items.

Observed unrelated Editor issue: Play Mode initialization reports an unsupported
shader pass in `SubwooferShaderWarmup.WarmUpShaders`. The loopback checks can run
despite this; this change does not alter that shader warmup.
