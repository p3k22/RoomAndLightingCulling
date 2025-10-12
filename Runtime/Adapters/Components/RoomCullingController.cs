namespace P3k.RoomAndLightingCulling.Adapters.Components
{
   using P3k.RoomAndLightingCulling.Implementations.DataContainers;
   using P3k.RoomAndLightingCulling.Implementations.Services;

   using System.Collections;
   using System.Collections.Generic;
   using System.Linq;

   using UnityEngine;
   using UnityEngine.InputSystem;

   [DisallowMultipleComponent]
   public sealed class RoomCullingController : MonoBehaviour
   {
      /// <summary>
      ///    Single orchestration service.
      /// </summary>
      private RoomCullingService _roomCullingService;

      private Rect _win = new(Screen.width - 400, Screen.height - 340, 390, 320);

      /// <summary>
      ///    Shared config source.
      /// </summary>
      public RoomCullingConfig Config => _config;

      /// <summary>
      ///    Exposes enabled rooms for UI or debug.
      /// </summary>
      public IReadOnlyList<RoomRendererRecord> EnabledRooms => _roomCullingService?.EnabledRooms;

      private void Awake()
      {
         if (Config == null)
         {
            enabled = false;
            return;
         }

         _roomCullingService = new RoomCullingService(Config, () => transform.position);

         if (_meshRoomsParent)
         {
            Initiate(_meshRoomsParent);
         }
      }

      private IEnumerator Start()
      {
         yield return new WaitForSeconds(1.5f);
         _win = new(Screen.width - 400, Screen.height - 340, 390, 320);
      }

      private void Update()
      {
         _roomCullingService?.Tick();

         if (Keyboard.current != null && Keyboard.current[_toggleKey].wasPressedThisFrame)
         {
            _isGUIVisible = !_isGUIVisible;
         }
      }

      private void OnGUI()
      {
         if (!_isGUIVisible)
         {
            return;
         }

         _win = GUI.Window(GetInstanceID(), _win, DrawWindow, $"Rooms Culling Values ({_toggleKey} - Show/Hide)");
      }

      /// <summary>
      ///    Forces all rooms off and zeros lights and shadows.
      /// </summary>
      public void ForceAllOff()
      {
         _roomCullingService?.ForceAllOff();
      }

      /// <summary>
      ///    Forces all rooms on.
      /// </summary>
      public void ForceAllOn()
      {
         _roomCullingService?.ForceAllOn();
      }

      /// <summary>
      ///    Re-collects rooms under the given parent and primes.
      /// </summary>
      public void Initiate(Transform roomsParent)
      {
         _meshRoomsParent = roomsParent;
         _roomCullingService?.InitiateCullingServices(roomsParent);
      }

      /// <summary>
      ///    Signals config changed and triggers a full sweep.
      /// </summary>
      public void OnConfigChanged()
      {
         _roomCullingService?.OnConfigChanged();
      }

      /// <summary>
      ///    Recomputes room centers from current transforms.
      /// </summary>
      public void RefreshCenters()
      {
         _roomCullingService?.RefreshCenters();
      }

      private static float FloatFieldWithSlider(string label, float value, float min, float max)
      {
         GUILayout.BeginHorizontal();
         GUILayout.Label(label, GUILayout.Width(180));
         value = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(120));
         var s = GUILayout.TextField(value.ToString("0.###"), GUILayout.Width(60));
         GUILayout.EndHorizontal();
         return float.TryParse(s, out var v) ? Mathf.Clamp(v, min, max) : value;
      }

      private static int IntFieldWithSlider(string label, int value, int min, int max)
      {
         GUILayout.BeginHorizontal();
         GUILayout.Label(label, GUILayout.Width(180));
         value = (int) GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(120));
         var s = GUILayout.TextField(value.ToString(), GUILayout.Width(60));
         GUILayout.EndHorizontal();
         return int.TryParse(s, out var v) ? Mathf.Clamp(v, min, max) : value;
      }

      private void DrawWindow(int id)
      {
         var cfg = Config;
         var oldChecks = cfg.ChecksPerFrame;
         var oldRoomEn = cfg.RoomEnableDistance;
         var oldRoomDis = cfg.RoomDisableDistance;
         var oldLightDis = cfg.LightDisableDistance;
         var oldLightMax = cfg.LightMaxIntensityDistance;
         var oldInnerPow = cfg.InnerFalloffPower;
         var oldShadows = cfg.ShadowsCount;
         var oldSoftShD = cfg.SoftShadowDistance;
         var oldMaxShD = cfg.MaxShadowDistance;
         var oldVert = cfg.VerticalTolerance;

         GUILayout.BeginVertical("box");

         cfg.ChecksPerFrame = IntFieldWithSlider(" Checks Per Frame", cfg.ChecksPerFrame, 1, 2048);
         cfg.RoomEnableDistance = FloatFieldWithSlider(" Room Enable Distance", cfg.RoomEnableDistance, 0f, 500f);
         cfg.RoomDisableDistance = FloatFieldWithSlider(" Room Disable Distance", cfg.RoomDisableDistance, 0f, 800f);
         cfg.LightDisableDistance = FloatFieldWithSlider(" Light Disable Distance", cfg.LightDisableDistance, 0f, 800f);
         cfg.LightMaxIntensityDistance = FloatFieldWithSlider(
         " Light Max Intensity Dist",
         cfg.LightMaxIntensityDistance,
         1f,
         500f);
         cfg.InnerFalloffPower = FloatFieldWithSlider(" Inner Falloff Power", cfg.InnerFalloffPower, 1f, 8f);
         cfg.ShadowsCount = IntFieldWithSlider(" Shadows Count", cfg.ShadowsCount, 0, 128);
         cfg.SoftShadowDistance = FloatFieldWithSlider(" Soft Shadows Distance", cfg.SoftShadowDistance, 0f, 500f);
         cfg.MaxShadowDistance = FloatFieldWithSlider(
         " Max Shadows Distance",
         cfg.MaxShadowDistance,
         cfg.SoftShadowDistance,
         500f);
         cfg.VerticalTolerance = FloatFieldWithSlider(" Vertical Tolerance", cfg.VerticalTolerance, 0f, 500f);

         GUILayout.EndVertical();
         GUI.DragWindow();

         if (cfg.ChecksPerFrame != oldChecks || !Mathf.Approximately(cfg.RoomEnableDistance, oldRoomEn)
             || !Mathf.Approximately(cfg.RoomDisableDistance, oldRoomDis)
             || !Mathf.Approximately(cfg.LightDisableDistance, oldLightDis)
             || !Mathf.Approximately(cfg.LightMaxIntensityDistance, oldLightMax)
             || !Mathf.Approximately(cfg.InnerFalloffPower, oldInnerPow) || cfg.ShadowsCount != oldShadows
             || !Mathf.Approximately(cfg.SoftShadowDistance, oldSoftShD)
             || !Mathf.Approximately(cfg.MaxShadowDistance, oldMaxShD)
             || !Mathf.Approximately(cfg.VerticalTolerance, oldVert))
         {
            OnConfigChanged();
         }
      }

   #region SerialisedFields

      [Header("GUI")]
      [SerializeField]
      private bool _isGUIVisible = true;

      [SerializeField]
      private Key _toggleKey = Key.F6;

      [Header("Configuration")]
      [SerializeField]
      private RoomCullingConfig _config;

      [SerializeField]
      private Transform _meshRoomsParent;

   #endregion
   }
}