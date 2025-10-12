namespace P3k.RoomAndLightingCulling.Implementations.DataContainers
{
   using System;
   using System.Linq;

   using UnityEngine;

   [Serializable]
   public class RoomCullingConfig
   {
   #region Public Fields

      public float RoomEnableDistance = 80;

      public float RoomDisableDistance = 100f;

      public float LightEnableDistance = 30f;

      public float LightDisableDistance = 40f;

      public float LightMaxIntensityDistance = 20f;

      public float InnerFalloffPower = 1f;

      public float VerticalTolerance = 10f;

      public int ChecksPerFrame = 64;

      public int ShadowsCount = 12;

      public float MaxShadowDistance = 60f;

      public float SoftShadowDistance = 50f;

      public bool ApplyMaxShadowDistanceToRenderPipeline = true;

   #endregion

      public void ValidateValues()
      {
         ChecksPerFrame = Mathf.Max(1, ChecksPerFrame);

         RoomEnableDistance = Mathf.Max(0f, RoomEnableDistance);
         RoomDisableDistance = Mathf.Max(RoomEnableDistance, RoomDisableDistance);

         LightEnableDistance = Mathf.Max(0f, LightEnableDistance);
         LightDisableDistance = Mathf.Max(LightEnableDistance, LightDisableDistance);

         LightMaxIntensityDistance = Mathf.Max(0f, LightMaxIntensityDistance);
         LightDisableDistance = Mathf.Max(LightDisableDistance, LightMaxIntensityDistance);

         InnerFalloffPower = Mathf.Max(1f, InnerFalloffPower);

         VerticalTolerance = Mathf.Max(0f, VerticalTolerance);

         SoftShadowDistance = Mathf.Max(0f, SoftShadowDistance);
         MaxShadowDistance = Mathf.Max(SoftShadowDistance, MaxShadowDistance);

         ShadowsCount = Mathf.Max(0, ShadowsCount);
      }
   }
}
