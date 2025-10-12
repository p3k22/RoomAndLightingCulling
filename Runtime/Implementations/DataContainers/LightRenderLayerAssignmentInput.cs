namespace P3k.RoomAndLightingCulling.Implementations.DataContainers
{
   using System.Linq;

   using UnityEngine;

   internal readonly struct LightRenderLayerAssignmentInput
   {
      internal readonly float CoolDownSeconds;

      internal readonly bool IncludeInactiveChildren;

      internal readonly float MinSeparation;

      internal readonly float MoveGate;

      internal readonly float Now;

      internal readonly Vector3? PlayerPosition;

      internal readonly float RadiusIn;

      internal readonly float RadiusOut;

      internal readonly Transform RoomsRoot;

      internal bool DynamicMode => PlayerPosition.HasValue && RadiusIn > 0f;

      internal LightRenderLayerAssignmentInput(
         Transform roomsRoot,
         float minSeparation,
         bool includeInactiveChildren = false,
         Vector3? playerPosition = null,
         float radiusIn = 0f,
         float radiusOut = 0f,
         float moveGate = 0.5f,
         float coolDownSeconds = 0.5f,
         float now = -1f)
      {
         RoomsRoot = roomsRoot;
         MinSeparation = minSeparation;
         IncludeInactiveChildren = includeInactiveChildren;
         PlayerPosition = playerPosition;
         RadiusIn = radiusIn;
         RadiusOut = Mathf.Max(radiusOut, radiusIn);
         MoveGate = moveGate;
         CoolDownSeconds = coolDownSeconds;
         Now = now;
      }
   }
}
