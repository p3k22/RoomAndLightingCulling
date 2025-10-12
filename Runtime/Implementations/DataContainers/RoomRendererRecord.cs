namespace P3k.RoomAndLightingCulling.Implementations.DataContainers
{
   using P3k.RoomAndLightingCulling.Abstractions.Enums;

   using System.Collections.Generic;
   using System.Linq;

   using UnityEngine;

   /// <summary>
   ///    Immutable data holder for one room.
   /// </summary>
   public sealed class RoomRendererRecord
   {
      /// <summary>
      ///    Cached center for distance checks.
      /// </summary>
      public Vector3 Center { get; set; }

      /// <summary>
      ///    All lights under this room.
      /// </summary>
      public List<Light> Lights { get; }

      /// <summary>
      ///    All mesh renderers under this room.
      /// </summary>
      public List<MeshRenderer> Renderers { get; }

      /// <summary>
      ///    Current enabled/disabled state.
      /// </summary>
      public RoomRenderState RenderState { get; set; }

      /// <summary>
      ///    Room root transform.
      /// </summary>
      public Transform Root { get; }

      /// <summary>
      ///    Ctor.
      /// </summary>
      public RoomRendererRecord(Transform root, List<MeshRenderer> renderers, List<Light> lights)
      {
         Root = root;
         Renderers = renderers;
         Lights = lights;
         Center = root ? root.position : Vector3.zero;
         RenderState = RoomRenderState.Off;
      }
   }
}
