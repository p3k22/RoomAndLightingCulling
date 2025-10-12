namespace P3k.RoomAndLightingCulling.Implementations.Utilities
{
   using System.Linq;

   using UnityEngine;
   using UnityEngine.Rendering;
   using UnityEngine.Rendering.Universal;

   public static class ShadowDistanceSetter
   {
      public static void Set(float distance)
      {
      #if HAS_URP
         var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
         if (urp != null)
         {
            urp.shadowDistance = distance;
            return;
         }
      #endif
         // Built-in RP fallback
         QualitySettings.shadowDistance = distance;
      }
   }
}
