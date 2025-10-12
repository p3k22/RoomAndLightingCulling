namespace P3k.RoomAndLightingCulling.Implementations.DataContainers
{
   using System.Linq;

   /// <summary>
   ///    LightIntensity tuning inputs.
   /// </summary>
   public struct RoomLightIntensityConfig
   {
      /// <summary>
      ///    Outer distance where light reaches zero intensity.
      /// </summary>
      public float DisableDistance;

      /// <summary>
      ///    Power for inner falloff curve.
      /// </summary>
      public float InnerFalloffPower;

      /// <summary>
      ///    Inner distance where intensity approaches original value.
      /// </summary>
      public float MaxIntensityDistance;
   }
}
