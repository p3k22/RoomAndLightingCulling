namespace P3k.RoomAndLightingCulling.Implementations.Utilities
{
   using P3k.RoomAndLightingCulling.Abstractions.Enums;
   using P3k.RoomAndLightingCulling.Implementations.Culling;
   using P3k.RoomAndLightingCulling.Implementations.DataContainers;

   using System.Linq;

   using UnityEngine;

   /// <summary>
   ///    Computes and applies per-light intensity.
   ///    Full intensity for d ≤ MaxIntensityDistance.
   ///    Smooth falloff to zero by DisableDistance.
   ///    Vertical gate optional.
   /// </summary>
   internal static class LightIntensityUtils
   {
      /// <summary>
      ///    Sets intensity for all lights in a room based on distance to camPos.
      ///    If vertical tolerance is active and a light is outside the band, intensity = 0 and shadows = None.
      /// </summary>
      internal static void Apply(
         RoomRendererRecord roomRenderer,
         Vector3 camPos,
         LightCache cache,
         in RoomLightIntensityConfig cfg)
      {
         if (roomRenderer == null || roomRenderer.RenderState == RoomRenderState.Off)
         {
            return;
         }

         var inner = Mathf.Max(0f, cfg.MaxIntensityDistance); // full intensity inside this
         var outer = Mathf.Max(inner, cfg.DisableDistance); // zero intensity at/after this
         var pow = Mathf.Max(1f, cfg.InnerFalloffPower); // used as falloff exponent

         for (var i = 0; i < roomRenderer.Lights.Count; i++)
         {
            var l = roomRenderer.Lights[i];
            if (!l)
            {
               continue;
            }

            cache.EnsureCached(l);
            var maxI = cache.GetOriginalIntensity(l);

            var d = Vector3.Distance(l.transform.position, camPos);

            float target;
            if (d <= inner)
            {
               // close = bright
               target = maxI;
            }
            else if (d >= outer)
            {
               // far = off
               target = 0f;
            }
            else
            {
               // fade from inner -> outer
               // t = 0 at inner, 1 at outer
               var t = (d - inner) / (outer - inner);
               var eased = 1f - Mathf.Pow(t, pow); // 1→0
               target = maxI * eased;
            }

            l.intensity = target;
            if (target <= 0f && l.shadows != LightShadows.None)
            {
               l.shadows = LightShadows.None;
            }
         }
      }

      /// <summary>
      ///    Zeros intensity and shadows for all lights in a room.
      /// </summary>
      internal static void ZeroAll(RoomRendererRecord roomRenderer, LightCache cache)
      {
         if (roomRenderer == null)
         {
            return;
         }

         for (var i = 0; i < roomRenderer.Lights.Count; i++)
         {
            var l = roomRenderer.Lights[i];
            if (!l)
            {
               continue;
            }

            cache.EnsureCached(l);
            l.intensity = 0f;
            if (l.shadows != LightShadows.None)
            {
               l.shadows = LightShadows.None;
            }
         }
      }
   }
}
