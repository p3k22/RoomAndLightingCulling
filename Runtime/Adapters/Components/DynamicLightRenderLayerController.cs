namespace P3k.RoomAndLightingCulling.Adapters.Components
{
   using P3k.RoomAndLightingCulling.Implementations.DataContainers;
   using P3k.RoomAndLightingCulling.Implementations.Services;

   using System.Collections.Generic;
   using System.Linq;

   using UnityEditor;

   using UnityEngine;

   [DisallowMultipleComponent]
   public sealed class DynamicLightRenderLayerController : MonoBehaviour
   {
      private float _nextTick;

      private DynamicLightRenderLayerAssignmentService _service;

      public int ColorCount => _colorCount;

      public int ConfiguredLightLayers => _configuredLightLayers;

      public int FirstBit => _firstBit;

      private void Start()
      {
         ClampColorCountToStoredConfig();
         _service = new DynamicLightRenderLayerAssignmentService(_firstBit, _colorCount, _configuredLightLayers);
         _nextTick = Time.time;
      }

      private void Update()
      {
         if (!_dynamicMode)
         {
            return;
         }

         if (!_meshRoomsParent)
         {
            return;
         }

         if (Time.time < _nextTick)
         {
            return;
         }

         _nextTick = Time.time + Mathf.Max(0.01f, _tickInterval);
         EnsureService();

         var input = new LightRenderLayerAssignmentInput(
         _meshRoomsParent,
         _minSeparation,
         _proximityFactor,
         _includeInactiveChildren,
         transform.position,
         _radiusIn,
         _radiusOut,
         _moveGate,
         _coolDownSeconds);

         _service.Assign(input);
      }

      public void AssignOnce()
      {
         EnsureService();
         var input = new LightRenderLayerAssignmentInput(
         _meshRoomsParent,
         _minSeparation,
         _proximityFactor,
         _includeInactiveChildren);
         _service.Assign(input);
      }

      public void Initiate(Transform meshRoomsParent)
      {
         _meshRoomsParent = meshRoomsParent;
      }

      private void ClampColorCountToStoredConfig()
      {
         var first = Mathf.Clamp(_firstBit, 1, 31);
         var bitBudget = Mathf.Max(0, 32 - first); // usable bits including 'first'
         var maxByBits = Mathf.Max(1, bitBudget);
         var maxByLayers = Mathf.Max(1, _configuredLightLayers);
         var maxUsable = Mathf.Min(maxByBits, maxByLayers);
         _colorCount = Mathf.Clamp(_colorCount, 1, maxUsable);
      }

      private void EnsureService()
      {
         if (_service == null)
         {
            ClampColorCountToStoredConfig();
            _service = new DynamicLightRenderLayerAssignmentService(_firstBit, _colorCount, _configuredLightLayers);
         }
      }

   #if UNITY_EDITOR
      private void OnValidate()
      {
         ClampColorCountToStoredConfig();
      }
   #endif

   #if UNITY_EDITOR
      private void OnDrawGizmos()
      {
         if (!Application.isPlaying || !_isShowingGizmos)
         {
            return;
         }

         // radii
         Gizmos.matrix = Matrix4x4.identity;
         var pos = transform.position;

         Gizmos.color = new Color(0f, 0.5f, 1f, 0.15f);
         Gizmos.DrawSphere(pos, Mathf.Max(0f, _radiusOut));
         Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
         Gizmos.DrawSphere(pos, Mathf.Max(0f, _radiusIn));

         Gizmos.color = new Color(0f, 0f, 0f, 1f);
         Gizmos.DrawWireSphere(pos, Mathf.Max(0f, _radiusOut));
         Gizmos.color = new Color(0f, 0.7f, 0f, 1f);
         Gizmos.DrawWireSphere(pos, Mathf.Max(0f, _radiusIn));

         if (!_meshRoomsParent)
         {
            return;
         }

         // collect rooms and bounds
         var rooms = new List<(Transform t, Bounds b, uint mask, int colorIndex)>();
         for (var i = 0; i < _meshRoomsParent.childCount; i++)
         {
            var room = _meshRoomsParent.GetChild(i);
            var b = ComputeRoomBounds(room, _includeInactiveChildren, out var hasAny);
            if (!hasAny)
            {
               b = new Bounds(room.position, Vector3.one * 0.1f);
            }

            var mask = ReadRoomRenderingLayerMask(room, _includeInactiveChildren);
            var colorIndex = MaskToColorIndex(mask, _firstBit, _colorCount);
            rooms.Add((room, b, mask, colorIndex));
         }

         // draw bounds (wire) with color by assigned mask; fill faintly
         for (var i = 0; i < rooms.Count; i++)
         {
            var r = rooms[i];
            var col = ColorForIndex(r.colorIndex, _colorCount);
            var wire = col;
            wire.a = 1f;
            var fill = col;
            fill.a = 0.08f;

            Gizmos.color = fill;
            Gizmos.DrawCube(r.b.center, r.b.size);

            Gizmos.color = wire;
            Gizmos.DrawWireCube(r.b.center, r.b.size);

         #if UNITY_EDITOR
            // labels
            Handles.color = wire;
            var label = r.mask != 0u ? $" Light Layer Index: {Mathf.RoundToInt(Mathf.Log(r.mask, 2))}" : " unassigned";
            Handles.Label(r.b.center + (Vector3.up * (0.25f + r.b.extents.y)), new GUIContent(label));
         #endif
         }

         // conflicts (minSeparation) as lines
         Gizmos.color = new Color(1f, 0.6f, 0f, 1f);
         for (var a = 0; a < rooms.Count; a++)
         {
            for (var b = a + 1; b < rooms.Count; b++)
            {
               if (AABBtoAABBDistance(rooms[a].b, rooms[b].b) < _minSeparation)
               {
                  var p0 = ClosestPointOnBounds(rooms[a].b, rooms[b].b.center);
                  var p1 = ClosestPointOnBounds(rooms[b].b, rooms[a].b.center);
                  Gizmos.DrawLine(p0, p1);
               }
            }
         }
      }

      private static Bounds ComputeRoomBounds(Transform root, bool includeInactiveChildren, out bool hasAny)
      {
         hasAny = false;
         var renderers = root.GetComponentsInChildren<MeshRenderer>(includeInactiveChildren);
         Bounds b = default;

         if (renderers != null && renderers.Length > 0)
         {
            for (var i = 0; i < renderers.Length; i++)
            {
               var mr = renderers[i];
               if (!mr)
               {
                  continue;
               }

               if (!hasAny)
               {
                  b = mr.bounds;
                  hasAny = true;
               }
               else
               {
                  b.Encapsulate(mr.bounds);
               }
            }
         }

         if (!hasAny)
         {
            var bc = root.GetComponentInChildren<BoxCollider>(includeInactiveChildren);
            if (bc)
            {
               b = bc.bounds;
               hasAny = true;
            }
         }

         if (!hasAny)
         {
            b = new Bounds(root.position, Vector3.one * 0.1f);
         }

         return b;
      }

      private static uint ReadRoomRenderingLayerMask(Transform root, bool includeInactiveChildren)
      {
         // prefer renderer mask; if mixed, pick the most common mask
         var renderers = root.GetComponentsInChildren<MeshRenderer>(includeInactiveChildren);
         if (renderers == null || renderers.Length == 0)
         {
            return 0u;
         }

         var counts = new Dictionary<uint, int>();
         for (var i = 0; i < renderers.Length; i++)
         {
            var r = renderers[i];
            if (!r)
            {
               continue;
            }

            var m = r.renderingLayerMask;
            if (!counts.TryGetValue(m, out var c))
            {
               counts[m] = 1;
            }
            else
            {
               counts[m] = c + 1;
            }
         }

         if (counts.Count == 0)
         {
            return 0u;
         }

         return counts.OrderByDescending(kv => kv.Value).First().Key;
      }

      private static int MaskToColorIndex(uint mask, int firstBit, int colorCount)
      {
         if (mask == 0u)
         {
            return -1;
         }

         for (var i = 0; i < colorCount; i++)
         {
            var bit = firstBit + i;
            var m = 1u << bit;
            if ((mask & m) != 0u)
            {
               return i;
            }
         }

         return -1;
      }

      private static Color ColorForIndex(int idx, int count)
      {
         if (idx < 0 || count <= 0)
         {
            return new Color(0.2f, 0.2f, 0.2f, 1f);
         }

         var h = Mathf.Repeat((float) idx / Mathf.Max(1, count), 1f);
         var col = Color.HSVToRGB(h, 0.85f, 1f);
         return col;
      }

      private static float AABBtoAABBDistance(in Bounds a, in Bounds b)
      {
         var dx = Mathf.Max(0f, Mathf.Max(a.min.x - b.max.x, b.min.x - a.max.x));
         var dy = Mathf.Max(0f, Mathf.Max(a.min.y - b.max.y, b.min.y - a.max.y));
         var dz = Mathf.Max(0f, Mathf.Max(a.min.z - b.max.z, b.min.z - a.max.z));
         return Mathf.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
      }

      private static Vector3 ClosestPointOnBounds(in Bounds b, in Vector3 p)
      {
         var x = Mathf.Clamp(p.x, b.min.x, b.max.x);
         var y = Mathf.Clamp(p.y, b.min.y, b.max.y);
         var z = Mathf.Clamp(p.z, b.min.z, b.max.z);
         return new Vector3(x, y, z);
      }
   #endif

   #region SerialisedFields

      [SerializeField]
      private bool _isShowingGizmos;

      [SerializeField]
      private int _colorCount = 8;

      [SerializeField]
      [HideInInspector]
      private int _configuredLightLayers = 8; // set by editor only

      [SerializeField]
      private float _coolDownSeconds = 0.5f;

      [SerializeField]
      private Transform _meshRoomsParent;

      [SerializeField]
      private bool _dynamicMode = true;

      [SerializeField]
      private int _firstBit = 1; // 1..31

      [SerializeField]
      private bool _includeInactiveChildren = true;

      [SerializeField]
      private float _minSeparation = 5f;

      [SerializeField]
      private int _proximityFactor = 3;

      [SerializeField]
      private float _moveGate = 0.5f;

      [SerializeField]
      private float _radiusIn = 25;

      [SerializeField]
      private float _radiusOut = 75;

      [SerializeField]
      private float _tickInterval = 0.1f;

   #endregion
   }
}