namespace P3k.RoomAndLightingCulling.Implementations.Services
{
   using P3k.RoomAndLightingCulling.Implementations.DataContainers;

   using System.Collections.Generic;
   using System.Linq;

   using UnityEngine;
#if HAS_URP
   using UnityEngine.Rendering.Universal;
#endif

   internal sealed class DynamicLightRenderLayerAssignmentService
   {
      private Bounds[] _bounds = System.Array.Empty<Bounds>();

      private readonly int _colorCount;

      private List<int>[] _conflicts = System.Array.Empty<List<int>>();

      private readonly Dictionary<Transform, float> _cooldownUntil = new();

      private readonly Dictionary<Transform, uint> _currentMaskByRoom = new();

      private readonly int _firstBit;

      // proximity configuration: treat rooms within _proximityFactor * minSeparation as nearby
      private float _proximityFactor = 1.5f;

      private float _minSeparationUsed = 0f;

      private bool _haveLastPlayerPos;

      private Vector3 _lastPlayerPos;

      private readonly List<Transform> _roomKeys = new();

      private readonly Dictionary<Transform, (List<MeshRenderer> renderers, List<Light> lights)> _rooms = new();

      internal DynamicLightRenderLayerAssignmentService(int firstBit, int colorCount, int configuredLightLayers)
      {
         _firstBit = Mathf.Clamp(firstBit, 1, 31);

         var maxBits = Mathf.Max(1, (31 - _firstBit) + 1);
         var cap = Mathf.Clamp(Mathf.Max(1, configuredLightLayers), 1, maxBits);

         _colorCount = Mathf.Clamp(colorCount, 1, cap);
      }

      internal void Assign(in LightRenderLayerAssignmentInput input)
      {
         if (!input.RoomsRoot)
         {
            return;
         }

         CollectRooms(input.RoomsRoot, input.IncludeInactiveChildren);
         if (_rooms.Count == 0)
         {
            return;
         }

         ComputeBounds(input.IncludeInactiveChildren);
         BuildConflictGraph(input.MinSeparation, input.ProximityFactor);

         if (!input.DynamicMode)
         {
            GlobalAssign();
            return;
         }

         var pos = input.PlayerPosition!.Value;
         var movedEnough =
            !_haveLastPlayerPos || (pos - _lastPlayerPos).sqrMagnitude >= input.MoveGate * input.MoveGate;
         if (!movedEnough && _haveLastPlayerPos)
         {
            return;
         }

         var nowTime = input.Now >= 0f ? input.Now : Time.time;

         var inside = new List<int>();
         var border = new List<int>();
         var outside = new List<int>();

         for (var i = 0; i < _roomKeys.Count; i++)
         {
            var d = DistancePointToAABB(pos, _bounds[i]);
            if (d <= input.RadiusIn)
            {
               inside.Add(i);
            }
            else if (d <= input.RadiusOut)
            {
               border.Add(i);
            }
            else
            {
               outside.Add(i);
            }
         }

         var assigned = SnapshotCurrent();

         var orderInside = inside.OrderByDescending(i => _conflicts[i].Count).ToList();

         // Prepare allowed masks and track used masks within the 'inside' set so we can prefer uniqueness
         var allowedMasks = GetAllowedMasks().ToList();
         var usedInInside = new HashSet<uint>();
         for (var i = 0; i < inside.Count; i++)
         {
            var idx = inside[i];
            var m = assigned[idx];
            if (m != 0u && IsAllowedMask(m))
            {
               usedInInside.Add(m);
            }
         }

         for (var k = 0; k < orderInside.Count; k++)
         {
            var idx = orderInside[k];
            var banned = GetBannedMasksLocal(idx, assigned, inside);

            // prefer an allowed mask that isn't banned and isn't already used by another inside-room
            var prefer = 0u;
            for (var ai = 0; ai < allowedMasks.Count; ai++)
            {
               var cand = allowedMasks[ai];
               if (!banned.Contains(cand) && !usedInInside.Contains(cand))
               {
                  prefer = cand;
                  break;
               }
            }

            // choose the best mask for this room taking proximity usage into account
            var chosen = ChooseBestMaskForRoom(idx, banned, prefer, assigned);
            if (chosen == 0u)
            {
               continue;
            }

            assigned[idx] = chosen;
            usedInInside.Add(chosen);
         }

         for (var k = 0; k < border.Count; k++)
         {
            var idx = border[k];
            var keep = assigned[idx];
            var collides = false;
            for (var n = 0; n < _conflicts[idx].Count; n++)
            {
               var j = _conflicts[idx][n];
               if (assigned[j] == keep && inside.Contains(j))
               {
                  collides = true;
                  break;
               }
            }

            if (!collides && keep != 0u)
            {
               continue;
            }

            var banned = GetBannedMasksLocal(idx, assigned, inside);
            var chosen = ChooseMask(banned, keep);
            assigned[idx] = chosen == 0u ? keep : chosen;
         }

         for (var k = 0; k < outside.Count; k++)
         {
            var idx = outside[k];
            if (assigned[idx] == 0u)
            {
               var chosen = ChooseMask(GetBannedMasks(idx, assigned));
               assigned[idx] = chosen == 0u ? ChooseMask(new HashSet<uint>()) : chosen;
            }
         }

         ApplyDeltas(assigned, nowTime, input.CoolDownSeconds);

         _lastPlayerPos = pos;
         _haveLastPlayerPos = true;
      }

      private static float AABBtoAABBDistance(in Bounds a, in Bounds b)
      {
         var dx = Mathf.Max(0f, Mathf.Max(a.min.x - b.max.x, b.min.x - a.max.x));
         var dy = Mathf.Max(0f, Mathf.Max(a.min.y - b.max.y, b.min.y - a.max.y));
         var dz = Mathf.Max(0f, Mathf.Max(a.min.z - b.max.z, b.min.z - a.max.z));
         return Mathf.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
      }

      private static float DistancePointToAABB(Vector3 p, in Bounds b)
      {
         var x = Mathf.Max(b.min.x - p.x, 0f, p.x - b.max.x);
         var y = Mathf.Max(b.min.y - p.y, 0f, p.y - b.max.y);
         var z = Mathf.Max(b.min.z - p.z, 0f, p.z - b.max.z);
         return Mathf.Sqrt((x * x) + (y * y) + (z * z));
      }

      private void ApplyDeltas(uint[] assigned, float nowTime, float coolDownSeconds)
      {
         for (var i = 0; i < _roomKeys.Count; i++)
         {
            var t = _roomKeys[i];
            var newMask = assigned[i];
            _currentMaskByRoom.TryGetValue(t, out var oldMask);
            if (newMask == 0u || newMask == oldMask)
            {
               continue;
            }

            if (_cooldownUntil.TryGetValue(t, out var until) && nowTime < until)
            {
               continue;
            }

            ApplyRenderLayerMask(i, newMask);
            _currentMaskByRoom[t] = newMask;
            _cooldownUntil[t] = nowTime + Mathf.Max(0.01f, coolDownSeconds);
         }
      }

      private void ApplyRenderLayerMask(int roomIndex, uint mask)
      {
         var room = _roomKeys[roomIndex];
         var (renderers, lights) = _rooms[room];

      #if HAS_URP
         for (var i = 0; i < renderers.Count; i++)
         {
            var r = renderers[i];
            if (!r)
            {
               continue;
            }

            r.renderingLayerMask = mask;
         }

         for (var i = 0; i < lights.Count; i++)
         {
            var l = lights[i];
            if (!l)
            {
               continue;
            }

            var data = l.GetUniversalAdditionalLightData();
            if (data != null)
            {
               data.renderingLayers = mask;
            }
         }
      #else
         // Built-in RP: no rendering layer effects.
         for (var i = 0; i < renderers.Count; i++)
         {
            var r = renderers[i];
            if (r)
            {
               r.renderingLayerMask = mask; // harmless write
            }
         }
      #endif
      }

      private void BuildConflictGraph(float minSeparation, int proximityFactor)
      {
         _minSeparationUsed = Mathf.Max(0f, minSeparation);
         _proximityFactor = Mathf.Max(0f, proximityFactor);

         var n = _roomKeys.Count;
         _conflicts = new List<int>[n];
         for (var i = 0; i < n; i++)
         {
            _conflicts[i] = new List<int>();
         }

         for (var a = 0; a < n; a++)
         {
            for (var b = a + 1; b < n; b++)
            {
               if (AABBtoAABBDistance(_bounds[a], _bounds[b]) < minSeparation)
               {
                  _conflicts[a].Add(b);
                  _conflicts[b].Add(a);
               }
            }
         }
      }

      private List<int> BuildOrderByDegree()
      {
         var order = new List<int>(_roomKeys.Count);
         for (var i = 0; i < _roomKeys.Count; i++)
         {
            order.Add(i);
         }

         order.Sort((a, b) => _conflicts[b].Count.CompareTo(_conflicts[a].Count));
         return order;
      }

      private uint ChooseMask(HashSet<uint> banned, uint prefer = 0u)
      {
         if (prefer != 0u && !banned.Contains(prefer) && IsAllowedMask(prefer))
         {
            return prefer;
         }

         for (var c = 0; c < _colorCount; c++)
         {
            var bit = _firstBit + c;
            var m = 1u << bit;
            if (!banned.Contains(m))
            {
               return m;
            }
         }

         return 0u;
      }

      private uint ChooseBestMaskForRoom(int idx, HashSet<uint> banned, uint prefer, uint[] assigned)
      {
         if (prefer != 0u && !banned.Contains(prefer) && IsAllowedMask(prefer))
         {
            return prefer;
         }

         var best = 0u;
         var bestScore = int.MaxValue;

         for (var c = 0; c < _colorCount; c++)
         {
            var bit = _firstBit + c;
            var cand = 1u << bit;
            if (banned.Contains(cand))
            {
               continue;
            }

            // score = number of nearby rooms already using this mask (lower is better)
            var score = 0;
            for (var j = 0; j < _roomKeys.Count; j++)
            {
               if (j == idx)
               {
                  continue;
               }

               if (assigned[j] != cand)
               {
                  continue;
               }

               if (IsWithinProximity(idx, j))
               {
                  score++;
               }
            }

            if (score < bestScore)
            {
               bestScore = score;
               best = cand;

               if (score == 0)
               {
                  break; // can't beat zero
               }
            }
         }

         return best;
      }

      private bool IsWithinProximity(int a, int b)
      {
         if (_minSeparationUsed <= 0f)
         {
            return false;
         }

         var threshold = _minSeparationUsed * _proximityFactor;
         return AABBtoAABBDistance(_bounds[a], _bounds[b]) < threshold;
      }

      private void CollectRooms(Transform root, bool includeInactiveChildren)
      {
         _rooms.Clear();
         _roomKeys.Clear();

         for (var i = 0; i < root.childCount; i++)
         {
            var roomRoot = root.GetChild(i);
            var mrs = roomRoot.GetComponentsInChildren<MeshRenderer>(includeInactiveChildren);
            var ls = roomRoot.GetComponentsInChildren<Light>(includeInactiveChildren);

            var renderers = mrs != null ? mrs.ToList() : new List<MeshRenderer>();
            var lights = ls != null ? ls.ToList() : new List<Light>();

            _rooms[roomRoot] = (renderers, lights);
            _roomKeys.Add(roomRoot);
         }
      }

      private void ComputeBounds(bool includeInactiveChildren)
      {
         var n = _roomKeys.Count;
         _bounds = new Bounds[n];
         var has = new bool[n];

         for (var i = 0; i < n; i++)
         {
            var roomRoot = _roomKeys[i];
            var (renderers, _) = _rooms[roomRoot];

            for (var m = 0; m < renderers.Count; m++)
            {
               var mr = renderers[m];
               if (!mr)
               {
                  continue;
               }

               if (!has[i])
               {
                  _bounds[i] = mr.bounds;
                  has[i] = true;
               }
               else
               {
                  _bounds[i].Encapsulate(mr.bounds);
               }
            }

            if (!has[i])
            {
               var bc = roomRoot.GetComponentInChildren<BoxCollider>(includeInactiveChildren);
               if (bc)
               {
                  _bounds[i] = bc.bounds;
                  has[i] = true;
               }
            }

            if (!has[i])
            {
               var p = roomRoot.position;
               _bounds[i] = new Bounds(p, Vector3.one * 0.01f);
            }
         }
      }

      private HashSet<uint> GetBannedMasks(int idx, uint[] assigned)
      {
         var set = new HashSet<uint>();
         var neigh = _conflicts[idx];
         for (var i = 0; i < neigh.Count; i++)
         {
            var m = assigned[neigh[i]];
            if (m != 0u && IsAllowedMask(m))
            {
               set.Add(m);
            }
         }

         // also ban masks used by any room within proximity threshold
         if (_minSeparationUsed > 0f)
         {
            var threshold = _minSeparationUsed * _proximityFactor;
            for (var j = 0; j < _roomKeys.Count; j++)
            {
               if (j == idx)
               {
                  continue;
               }

               if (AABBtoAABBDistance(_bounds[idx], _bounds[j]) < threshold)
               {
                  var m = assigned[j];
                  if (m != 0u && IsAllowedMask(m))
                  {
                     set.Add(m);
                  }
               }
            }
         }

         return set;
      }

      private HashSet<uint> GetBannedMasksLocal(int idx, uint[] assigned, List<int> localSet)
      {
         var set = new HashSet<uint>();
         var neigh = _conflicts[idx];
         for (var i = 0; i < neigh.Count; i++)
         {
            var j = neigh[i];
            if (!localSet.Contains(j))
            {
               continue;
            }

            var m = assigned[j];
            if (m != 0u && IsAllowedMask(m))
            {
               set.Add(m);
            }
         }

         // also ban masks used by any room in `localSet` or within proximity to idx
         if (_minSeparationUsed > 0f)
         {
            var threshold = _minSeparationUsed * _proximityFactor;
            for (var j = 0; j < _roomKeys.Count; j++)
            {
               if (localSet.Contains(j) || AABBtoAABBDistance(_bounds[idx], _bounds[j]) < threshold)
               {
                  var m = assigned[j];
                  if (m != 0u && IsAllowedMask(m))
                  {
                     set.Add(m);
                  }
               }
            }
         }

         return set;
      }

      private void GlobalAssign()
      {
         var order = BuildOrderByDegree();
         var assigned = new uint[_roomKeys.Count];

         for (var oi = 0; oi < order.Count; oi++)
         {
            var idx = order[oi];
            var banned = GetBannedMasks(idx, assigned);
            var chosen = ChooseMask(banned);
            if (chosen == 0u)
            {
               continue;
            }

            assigned[idx] = chosen;
         }

         for (var i = 0; i < _roomKeys.Count; i++)
         {
            ApplyRenderLayerMask(i, assigned[i]);
            _currentMaskByRoom[_roomKeys[i]] = assigned[i];
         }
      }

      private uint[] SnapshotCurrent()
      {
         var assigned = new uint[_roomKeys.Count];
         for (var i = 0; i < _roomKeys.Count; i++)
         {
            var t = _roomKeys[i];

            // Prefer stored current mask if it's allowed
            if (_currentMaskByRoom.TryGetValue(t, out var stored) && IsAllowedMask(stored))
            {
               assigned[i] = stored;
               continue;
            }

            // Otherwise inspect the renderers in the room and pick the most common allowed single-bit mask
            var counts = new Dictionary<uint, int>();
            var (renderers, _) = _rooms[t];
            for (var r = 0; r < renderers.Count; r++)
            {
               var mr = renderers[r];
               if (!mr)
               {
                  continue;
               }

               var m = mr.renderingLayerMask;
               if (!IsAllowedMask(m))
               {
                  continue;
               }

               if (!counts.TryGetValue(m, out var c))
               {
                  counts[m] = 1;
               }
               else
               {
                  counts[m] = c + 1;
               }
            }

            if (counts.Count > 0)
            {
               assigned[i] = counts.OrderByDescending(kv => kv.Value).First().Key;
            }
            else
            {
               assigned[i] = 0u;
            }
         }

         return assigned;
      }

      // Helper: enumerate allowed single-bit masks based on configured first bit and color count
      private IEnumerable<uint> GetAllowedMasks()
      {
         for (var c = 0; c < _colorCount; c++)
         {
            yield return 1u << (_firstBit + c);
         }
      }

      // Helper: return true only for masks equal to one of the allowed single-bit masks
      private bool IsAllowedMask(uint m)
      {
         if (m == 0u)
         {
            return false;
         }

         for (var c = 0; c < _colorCount; c++)
         {
            if (m == 1u << (_firstBit + c))
            {
               return true;
            }
         }

         return false;
      }
   }
}
