# Room and Lighting Culling System

A Unity culling system that optimizes performance by dynamically enabling/disabling rooms and their lights based on distance from the player/camera.

## What It Does

- **Room Culling**: Automatically shows/hides rooms based on distance
- **Light Intensity Falloff**: Smoothly fades light intensity as you move away
- **Shadow Management**: Intelligently assigns shadows to the nearest lights only
- **Performance Optimized**: Time-sliced updates to avoid frame spikes
- **Dynamic Light Render Layer Assingment (URP Only)**: Dynamically assings rooms and their lights to same `Light Renderer Layer` with adjacent room awareness.

## Setup

1. Add `RoomCullingController` to a GameObject in your scene
2. Assign the config in the controller
3. Set `_meshRoomsParent` to the parent transform containing your rooms OR call `RoomCullingController.InitiateCullingServices(Transform roomsParent)`
4. Each room should be a child GameObject with:
   - MeshRenderer components for geometry
   - Light components for lighting

Alternativley you can use the Prefab stored in the `PackageResources` folder

## Key Configuration

Adjust the `RoomCullingConfig`:

- **Room Enable/Disable Distance**: When rooms turn on/off
- **Light Distances**: Controls light intensity falloff
- **Shadows Count**: Maximum number of shadowed lights
- **Checks Per Frame**: How many rooms to check each frame (higher = more responsive, lower = better performance)
- **Vertical Tolerance**: Ignore rooms on different floors

## Runtime GUI

Press **F6** to toggle the debug GUI (configurable in `RoomsCullingGui`)

Adjust all settings in real-time to fine-tune your scene.

## How It Works

### Room Culling
- Rooms turn **ON** when you get within `RoomEnableDistance`
- Rooms turn **OFF** when you exceed `RoomDisableDistance`
- Uses hysteresis to prevent flickering

### Light Intensity
- Full brightness within `LightMaxIntensityDistance`
- Smooth falloff to zero at `LightDisableDistance`
- Falloff curve controlled by `InnerFalloffPower`

### Shadow Selection
- Only the closest N lights cast shadows (set by `ShadowsCount`)
- Soft shadows within `SoftShadowDistance`
- Hard shadows up to `MaxShadowDistance`
- Auto-disables shadows beyond max distance

### Performance
Time-slicing spreads work across frames. Instead of checking all rooms every frame, it checks `ChecksPerFrame` rooms, cycling through the full list over time.

## API

```csharp
// Get the controller
var controller = GetComponent<RoomCullingController>();

// Force all rooms off
controller.ForceAllOff();

// Force all rooms on
controller.ForceAllOn();

// Rebuild room list (if you add/remove rooms at runtime)
controller.InitiateCullingServices(newRoomsParent);

// Get currently enabled rooms
var enabled = controller.EnabledRooms;
```

## Vertical Tolerance

Set `VerticalTolerance > 0` to disable rooms on different floors. If the vertical distance between the player and a room exceeds this value, that room is culled regardless of horizontal distance.

## Render Pipeline Support

Works with both Built-in and URP. When `ApplyMaxShadowDistanceToRenderPipeline` is enabled, it automatically sets the global shadow distance to match `MaxShadowDistance`.

## Tips

- Start with `ChecksPerFrame = 64` and adjust based on room count
- Use higher `RoomDisableDistance` than `RoomEnableDistance` to prevent flickering
- Keep `ShadowsCount` low (8-16) for best performance
- Increase `InnerFalloffPower` for sharper light falloff


## Render Layer Assignment (URP) 

Prevent light bleed between adjacent rooms by giving nearby rooms distinct **Light Rendering Layers**. Works globally or **dynamically** around the player.

## What it does

* `DynamicLightRenderLayerAssignmentService.cs` — Pure service. Assigns URP *Rendering Layer Masks* to room renderers and lights.
* `PlayerDynamicLightRenderLayerController.cs` — Runtime driver. Holds config, runs one-shot or dynamic assignment.
* `PlayerDLRLControllerEditor.cs` — Editor inspector. Reads `ProjectSettings/TagManager.asset > m_RenderingLayers`, stores count on the Controller, clamps config.

**How it clamps**

* Editor stores `configuredLightLayers` from TagManager. No runtime queries.
* `firstBit` sets the first usable layer bit; usable bit budget = `32 - firstBit`.
* `colorCount` is clamped to `min(configuredLightLayers, bit budget)` in both Mono and Service.

**Modes**

* **Global**: One-shot greedy coloring by conflict degree.
* **Dynamic**: Inside `radiusIn` around the player, rooms are forced to unique masks; border rooms try to keep masks unless they collide; outside rooms keep or receive any available mask. Optional cooldown avoids thrash.

**Adjacency**

* Rooms are nodes. Conflicts added when AABB-to-AABB distance `< minSeparation`.
