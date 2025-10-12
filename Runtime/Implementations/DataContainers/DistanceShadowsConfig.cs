namespace P3k.RoomAndLightingCulling.Implementations.DataContainers
{
   using System.Linq;

   /// <summary>
   ///    Shadow selection inputs with basic hysteresis.
   /// </summary>
   public struct DistanceShadowsConfig
   {
      /// <summary>
      ///    Force at least Low shadow resolution.
      /// </summary>
      public bool ForceLowResolution;

      /// <summary>
      ///    Enter distance for any shadows.
      /// </summary>
      public float MaxEnter;

      /// <summary>
      ///    Exit distance for any shadows.
      /// </summary>
      public float MaxExit;

      /// <summary>
      ///    Maximum number of shadowed lights.
      /// </summary>
      public int ShadowsCount;

      /// <summary>
      ///    Enter distance for soft shadows.
      /// </summary>
      public float SoftEnter;

      /// <summary>
      ///    Exit distance for soft shadows.
      /// </summary>
      public float SoftExit;
   }
}
