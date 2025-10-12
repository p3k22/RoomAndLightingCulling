namespace P3k.RoomAndLightingCulling.Implementations.Services
{
   using P3k.RoomAndLightingCulling.Abstractions.Enums;
   using P3k.RoomAndLightingCulling.Implementations.Culling;
   using P3k.RoomAndLightingCulling.Implementations.DataContainers;

   using System.Collections.Generic;
   using System.Linq;

   using UnityEngine;
   using UnityEngine.Rendering;

   /// <summary>
   ///    Picks nearest lights within thresholds and assigns Soft/Hard/None with hysteresis.
   /// </summary>
   internal sealed class ShadowSelectionService
   {
      /// <summary>
      ///    Reusable candidate list to avoid GC.
      /// </summary>
      private readonly List<(Light l, float d, LightShadows mode)> _candidates = new(128);

      /// <summary>
      ///    Applies shadow selection across enabled rooms.
      /// </summary>
      internal void Apply(
         IEnumerable<RoomRendererRecord> roomsOn,
         Vector3 camPos,
         LightCache cache,
         in DistanceShadowsConfig cfg)
      {
         if (cfg.ShadowsCount <= 0)
         {
            ForceNone(roomsOn);
            return;
         }

         _candidates.Clear();

         foreach (var room in roomsOn)
         {
            if (room == null || room.RenderState == RoomRenderState.Off)
            {
               continue;
            }

            var lights = room.Lights;
            for (var i = 0; i < lights.Count; i++)
            {
               var l = lights[i];
               if (!l)
               {
                  continue;
               }

               if (!cache.HadOriginalShadow(l))
               {
                  continue; // only lights that originally had shadows
               }

               if (l.intensity <= 0f)
               {
                  if (l.shadows != LightShadows.None)
                  {
                     l.shadows = LightShadows.None;
                  }

                  continue;
               }

               var d = Vector3.Distance(l.transform.position, camPos);

               if (d > cfg.MaxExit)
               {
                  if (l.shadows != LightShadows.None)
                  {
                     l.shadows = LightShadows.None;
                  }

                  continue;
               }

               var desired = d <= cfg.SoftEnter ? LightShadows.Soft :
                             d <= cfg.MaxEnter ? LightShadows.Hard : LightShadows.None;

               if (desired != LightShadows.None)
               {
                  _candidates.Add((l, d, desired));
               }
            }
         }

         if (_candidates.Count == 0)
         {
            // Nothing eligible
            ForceNone(roomsOn);
            return;
         }

         _candidates.Sort((a, b) => a.d.CompareTo(b.d));

         var allowed = Mathf.Min(cfg.ShadowsCount, _candidates.Count);
         for (var i = 0; i < _candidates.Count; i++)
         {
            var (l, _, mode) = _candidates[i];

            if (i < allowed)
            {
               if (cfg.ForceLowResolution && l.shadowResolution != LightShadowResolution.Low)
               {
                  l.shadowResolution = LightShadowResolution.Low;
               }

               if (l.shadows != mode)
               {
                  l.shadows = mode;
               }
            }
            else
            {
               if (l.shadows != LightShadows.None)
               {
                  l.shadows = LightShadows.None;
               }
            }
         }
      }

      /// <summary>
      ///    Sets shadows=None for all lights in enabled rooms.
      /// </summary>
      private static void ForceNone(IEnumerable<RoomRendererRecord> roomsOn)
      {
         foreach (var room in roomsOn)
         {
            if (room == null)
            {
               continue;
            }

            var lights = room.Lights;
            for (var i = 0; i < lights.Count; i++)
            {
               var l = lights[i];
               if (!l)
               {
                  continue;
               }

               if (l.shadows != LightShadows.None)
               {
                  l.shadows = LightShadows.None;
               }
            }
         }
      }
   }
}
