namespace P3k.RoomAndLightingCulling.Implementations.Utilities
{
   using P3k.RoomAndLightingCulling.Abstractions.Enums;
   using P3k.RoomAndLightingCulling.Implementations.DataContainers;

   using System.Linq;

   /// <summary>
   ///    Enables or disables all renderers for a room.
   /// </summary>
   internal static class RoomRendererUtils
   {
      /// <summary>
      ///    Sets all mesh renderers enabled/disabled. Updates state. Optionally zeroes lights when disabling.
      /// </summary>
      internal static void SetEnabled(
         RoomRendererRecord roomRenderer,
         bool isEnabled,
         System.Action<RoomRendererRecord> onDisabled = null)
      {
         if (roomRenderer == null)
         {
            return;
         }

         var renderers = roomRenderer.Renderers;
         for (var i = 0; i < renderers.Count; i++)
         {
            var r = renderers[i];
            if (r)
            {
               r.enabled = isEnabled;
            }
         }

         roomRenderer.RenderState = isEnabled ? RoomRenderState.On : RoomRenderState.Off;

         if (!isEnabled && onDisabled != null)
         {
            onDisabled(roomRenderer);
         }
      }
   }
}
