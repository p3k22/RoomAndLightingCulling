namespace P3k.RoomAndLightingCulling.Implementations.Culling
{
   using System.Collections.Generic;
   using System.Linq;

   using UnityEngine;

   /// <summary>
   ///    Caches original per-light properties for restoration and scaling.
   /// </summary>
   internal sealed class LightCache
   {
      /// <summary>
      ///    Whether the light originally had shadows.
      /// </summary>
      private readonly Dictionary<Light, bool> _hadShadows = new();

      /// <summary>
      ///    Original intensity per light.
      /// </summary>
      private readonly Dictionary<Light, float> _originalIntensities = new();

      /// <summary>
      ///    Ensures a light has cache entries populated.
      /// </summary>
      internal void EnsureCached(Light l)
      {
         if (!l)
         {
            return;
         }

         if (!_originalIntensities.ContainsKey(l))
         {
            _originalIntensities[l] = l.intensity;
         }

         if (!_hadShadows.ContainsKey(l))
         {
            _hadShadows[l] = l.shadows != LightShadows.None;
         }
      }

      /// <summary>
      ///    Gets original intensity with fallback to current if missing.
      /// </summary>
      internal float GetOriginalIntensity(Light l)
      {
         if (!l)
         {
            return 0f;
         }

         if (_originalIntensities.TryGetValue(l, out var v))
         {
            return v;
         }

         return l.intensity;
      }

      /// <summary>
      ///    Returns true if the light originally had shadows.
      /// </summary>
      internal bool HadOriginalShadow(Light l)
      {
         if (!l)
         {
            return false;
         }

         if (_hadShadows.TryGetValue(l, out var v))
         {
            return v;
         }

         return l.shadows != LightShadows.None;
      }
   }
}
