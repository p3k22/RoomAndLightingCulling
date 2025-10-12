namespace P3k.RoomAndLightingCulling.Implementations.Culling
{
   using P3k.RoomAndLightingCulling.Implementations.DataContainers;

   using System;
   using System.Collections.Generic;
   using System.Linq;

   using UnityEngine;

   /// <summary>
   ///    Collects and indexes rooms. Computes and refreshes centers.
   /// </summary>
   internal sealed class RoomRegistry
   {
      /// <summary>
      ///    Number of rooms.
      /// </summary>
      internal int Count => Rooms.Count;

      /// <summary>
      ///    Ordered rooms.
      /// </summary>
      internal List<RoomRendererRecord> Rooms { get; } = new();

      /// <summary>
      ///    Clears and rebuilds from a parent transform.
      /// </summary>
      internal void Rebuild(Transform root, bool includeInactiveChildren)
      {
         Rooms.Clear();

         if (!root)
         {
            return;
         }

         var childCount = root.childCount;
         for (var i = 0; i < childCount; i++)
         {
            var roomRoot = root.GetChild(i);
            if (!roomRoot)
            {
               continue;
            }

            var mrs = roomRoot.GetComponentsInChildren<MeshRenderer>(includeInactiveChildren)
                      ?? Array.Empty<MeshRenderer>();
            var ls = roomRoot.GetComponentsInChildren<Light>(includeInactiveChildren) ?? Array.Empty<Light>();

            var renderers = new List<MeshRenderer>(mrs.Length);
            for (var m = 0; m < mrs.Length; m++)
            {
               var r = mrs[m];
               if (r)
               {
                  renderers.Add(r);
               }
            }

            var lights = new List<Light>(ls.Length);
            for (var l = 0; l < ls.Length; l++)
            {
               var lt = ls[l];
               if (lt)
               {
                  lights.Add(lt);
               }
            }

            var rec = new RoomRendererRecord(roomRoot, renderers, lights);
            Rooms.Add(rec);
         }

         RefreshCenters();
      }

      /// <summary>
      ///    Recomputes center for every room from renderer bounds or root position fallback.
      /// </summary>
      internal void RefreshCenters()
      {
         for (var i = 0; i < Rooms.Count; i++)
         {
            var room = Rooms[i];
            var renderers = room.Renderers;
            if (renderers == null || renderers.Count == 0 || renderers.TrueForAll(r => !r))
            {
               room.Center = room.Root ? room.Root.position : Vector3.zero;
               continue;
            }

            var started = false;
            Bounds b = default;
            for (var j = 0; j < renderers.Count; j++)
            {
               var r = renderers[j];
               if (!r)
               {
                  continue;
               }

               if (!started)
               {
                  b = r.bounds;
                  started = true;
               }
               else
               {
                  b.Encapsulate(r.bounds);
               }
            }

            room.Center = started ? b.center : room.Root ? room.Root.position : Vector3.zero;
         }
      }
   }
}
