namespace P3k.RoomAndLightingCulling.Implementations.Services
{
   using P3k.RoomAndLightingCulling.Abstractions.Enums;
   using P3k.RoomAndLightingCulling.Implementations.Culling;
   using P3k.RoomAndLightingCulling.Implementations.DataContainers;
   using P3k.RoomAndLightingCulling.Implementations.Utilities;

   using System;
   using System.Collections.Generic;
   using System.Linq;

   using UnityEngine;

   /// <summary>
   ///    Single service that owns registry, intensity, and shadow selection. Stateless Mono uses this.
   /// </summary>
   internal sealed class RoomCullingService
   {
      /// <summary>
      ///    Shared config.
      /// </summary>
      private readonly RoomCullingConfig _config;

      /// <summary>
      ///    Position provider for the user/camera.
      /// </summary>
      private readonly Func<Vector3> _getUserPosition;

      /// <summary>
      ///    Original-light cache.
      /// </summary>
      private LightCache _lightCache;

      /// <summary>
      ///    Squared distance to disable a room.
      /// </summary>
      private float _roomDisableSqr;

      /// <summary>
      ///    Squared distance to enable a room.
      /// </summary>
      private float _roomEnableSqr;

      /// <summary>
      ///    _roomRegistry of rooms.
      /// </summary>
      private RoomRegistry _roomRegistry;

      /// <summary>
      ///    Shadow selector.
      /// </summary>
      private ShadowSelectionService _shadowSelection;

      /// <summary>
      ///    Time-slicing stepper.
      /// </summary>
      private readonly TimeSliceStepper _timeStepper;

      /// <summary>
      ///    Cached per-tick list of enabled rooms.
      /// </summary>
      internal List<RoomRendererRecord> EnabledRooms { get; }

      /// <summary>
      ///    Ctor.
      /// </summary>
      internal RoomCullingService(RoomCullingConfig config, Func<Vector3> getUserPosition)
      {
         _config = config;
         _getUserPosition = getUserPosition;

         EnabledRooms = new(128);
         _lightCache = new LightCache();
         _roomRegistry = new RoomRegistry();
         _shadowSelection = new ShadowSelectionService();
         _timeStepper = new TimeSliceStepper();

         _config.ValidateValues();
         RecomputeDistances();
      }

      /// <summary>
      ///    Forces all rooms off and zeros lights and shadows.
      /// </summary>
      internal void ForceAllOff()
      {
         for (var i = 0; i < _roomRegistry.Count; i++)
         {
            var room = _roomRegistry.Rooms[i];
            LightIntensityUtils.ZeroAll(room, _lightCache);
            RoomRendererUtils.SetEnabled(room, false);
         }
      }

      /// <summary>
      ///    Forces all rooms on. Intensities apply on next Tick.
      /// </summary>
      internal void ForceAllOn()
      {
         for (var i = 0; i < _roomRegistry.Count; i++)
         {
            var room = _roomRegistry.Rooms[i];
            RoomRendererUtils.SetEnabled(room, true);
         }
      }

      /// <summary>
      ///    Builds registry from a root and primes to all-off with a full sweep scheduled.
      /// </summary>
      internal void InitiateCullingServices(Transform roomsParent, bool includeInactiveChildren = false)
      {
         CollectRooms(roomsParent, includeInactiveChildren);
         PrimeInitial();
      }

      /// <summary>
      ///    Validates config and recomputes thresholds. Updates URP shadow distance. Starts full sweep.
      /// </summary>
      internal void OnConfigChanged()
      {
         _config.ValidateValues();
         RecomputeDistances();
         _timeStepper.StartFullSweep();
      }

      /// <summary>
      ///    Recomputes centers for all rooms.
      /// </summary>
      internal void RefreshCenters()
      {
         _roomRegistry.RefreshCenters();
      }

      /// <summary>
      ///    Per-frame or gated update. Time-slices room on/off checks then applies intensity and shadows.
      /// </summary>
      internal void Tick()
      {
         if (_roomRegistry.Count == 0)
         {
            return;
         }

         var userPosition = _getUserPosition();
         var useVert = _config.VerticalTolerance > 0f;
         var iterations = _timeStepper.GetIterations(_roomRegistry.Count, _config.ChecksPerFrame);

         for (var n = 0; n < iterations; n++)
         {
            var idx = _timeStepper.Next(_roomRegistry.Count);
            var room = _roomRegistry.Rooms[idx];

            if (room.Renderers == null || room.Renderers.Count == 0)
            {
               room.RenderState = RoomRenderState.Off;
               continue;
            }

            var c = room.Center;

            if (useVert && Mathf.Abs(c.y - userPosition.y) > _config.VerticalTolerance)
            {
               if (room.RenderState == RoomRenderState.On)
               {
                  RoomRendererUtils.SetEnabled(room, false);
               }

               continue;
            }

            var d2 = (c - userPosition).sqrMagnitude;

            if (room.RenderState == RoomRenderState.Off)
            {
               if (d2 <= _roomEnableSqr)
               {
                  RoomRendererUtils.SetEnabled(room, true);
               }
            }
            else
            {
               if (d2 >= _roomDisableSqr)
               {
                  RoomRendererUtils.SetEnabled(room, false, r => LightIntensityUtils.ZeroAll(r, _lightCache));
               }
            }

            if (room.RenderState == RoomRenderState.Off)
            {
               LightIntensityUtils.ZeroAll(room, _lightCache);
            }
         }

         _timeStepper.EndTick();

         ApplyIntensityAndShadows(userPosition);
      }

      /// <summary>
      ///    Applies per-light intensity to enabled rooms and shadow selection with hysteresis.
      /// </summary>
      private void ApplyIntensityAndShadows(Vector3 userPosition)
      {
         EnabledRooms.Clear();

         var intensityCfg = new RoomLightIntensityConfig
                               {
                                  MaxIntensityDistance = Mathf.Max(0f, _config.LightMaxIntensityDistance),
                                  DisableDistance =
                                     Mathf.Max(_config.LightMaxIntensityDistance, _config.LightDisableDistance),
                                  InnerFalloffPower = Mathf.Max(1f, _config.InnerFalloffPower),
                               };

         for (var i = 0; i < _roomRegistry.Count; i++)
         {
            var room = _roomRegistry.Rooms[i];
            if (room.RenderState == RoomRenderState.On)
            {
               EnabledRooms.Add(room);
               LightIntensityUtils.Apply(room, userPosition, _lightCache, intensityCfg);
            }
            else
            {
               LightIntensityUtils.ZeroAll(room, _lightCache);
            }
         }

         var softEnter = Mathf.Max(0f, _config.SoftShadowDistance);
         var softExit = softEnter + 1.0f;

         var maxEnter = Mathf.Max(softEnter, _config.MaxShadowDistance);
         var maxExit = maxEnter + 2.0f;

         var shadowCfg = new DistanceShadowsConfig
                            {
                               ShadowsCount = Mathf.Max(0, _config.ShadowsCount),
                               SoftEnter = softEnter,
                               SoftExit = softExit,
                               MaxEnter = maxEnter,
                               MaxExit = maxExit,
                               ForceLowResolution = true
                            };

         _shadowSelection.Apply(EnabledRooms, userPosition, _lightCache, shadowCfg);
      }

      /// <summary>
      ///    Rebuilds registry under a roomsParent and primes all rooms disabled.
      /// </summary>
      private void CollectRooms(Transform roomsParent, bool includeInactiveChildren)
      {
         _roomRegistry.Rebuild(roomsParent, includeInactiveChildren);

         for (var i = 0; i < _roomRegistry.Count; i++)
         {
            var room = _roomRegistry.Rooms[i];
            LightIntensityUtils.ZeroAll(room, _lightCache);
            RoomRendererUtils.SetEnabled(room, false);
         }

         _timeStepper.StartFullSweep();
      }

      /// <summary>
      ///    One-time priming around current position.
      /// </summary>
      private void PrimeInitial()
      {
         if (_roomRegistry.Count == 0)
         {
            return;
         }

         var userPosition = _getUserPosition();
         var useVert = _config.VerticalTolerance > 0f;

         for (var i = 0; i < _roomRegistry.Count; i++)
         {
            var room = _roomRegistry.Rooms[i];
            var c = room.Center;

            if (useVert && Mathf.Abs(c.y - userPosition.y) > _config.VerticalTolerance)
            {
               RoomRendererUtils.SetEnabled(room, false);
               continue;
            }

            var d2 = (c - userPosition).sqrMagnitude;
            var on = d2 <= _roomEnableSqr;
            RoomRendererUtils.SetEnabled(room, on);

            if (!on)
            {
               LightIntensityUtils.ZeroAll(room, _lightCache);
            }
         }

         ApplyIntensityAndShadows(userPosition);
      }

      /// <summary>
      ///    Computes squared thresholds and pushes URP shadow distance when requested.
      /// </summary>
      private void RecomputeDistances()
      {
         _roomEnableSqr = _config.RoomEnableDistance * _config.RoomEnableDistance;
         _roomDisableSqr = _config.RoomDisableDistance * _config.RoomDisableDistance;

         if (_config.ApplyMaxShadowDistanceToRenderPipeline)
         {
            ShadowDistanceSetter.Set(_config.MaxShadowDistance);
         }
      }
   }
}
