namespace P3k.RoomAndLightingCulling.Editor.CustomInspectors
{
   using P3k.RoomAndLightingCulling.Adapters.Components;

   using System.Linq;

   using UnityEditor;

   using UnityEngine;

   [CustomEditor(typeof(DynamicLightRenderLayerController))]
   public sealed class DynamicLightRenderLayerEditor : Editor
   {
      private SerializedProperty _configuredLightLayers;

      private SerializedProperty _isShowingGizmos,
                                 _meshRoomsParent,
                                 _firstBit,
                                 _colorCount,
                                 _minSeparation,
                                 _includeInactiveChildren;

      private SerializedProperty _dynamicMode, _radiusIn, _radiusOut, _moveGate, _coolDownSeconds, _tickInterval;

      private void OnEnable()
      {
         _isShowingGizmos = serializedObject.FindProperty("_isShowingGizmos");
         _meshRoomsParent = serializedObject.FindProperty("_meshRoomsParent");
         _firstBit = serializedObject.FindProperty("_firstBit");
         _colorCount = serializedObject.FindProperty("_colorCount");
         _minSeparation = serializedObject.FindProperty("_minSeparation");
         _includeInactiveChildren = serializedObject.FindProperty("_includeInactiveChildren");
         _dynamicMode = serializedObject.FindProperty("_dynamicMode");
         _radiusIn = serializedObject.FindProperty("_radiusIn");
         _radiusOut = serializedObject.FindProperty("_radiusOut");
         _moveGate = serializedObject.FindProperty("_moveGate");
         _coolDownSeconds = serializedObject.FindProperty("_coolDownSeconds");
         _tickInterval = serializedObject.FindProperty("_tickInterval");
         _configuredLightLayers = serializedObject.FindProperty("_configuredLightLayers");
      }

      public override void OnInspectorGUI()
      {
         serializedObject.Update();

         var layerCount = GetRenderingLayerCountFromTagManager(); // reads YAML you pasted
         _configuredLightLayers.intValue = Mathf.Clamp(layerCount, 1, 32);

         EditorGUILayout.LabelField("Gizmos", EditorStyles.whiteBoldLabel);
         EditorGUILayout.PropertyField(_isShowingGizmos);

         EditorGUILayout.Space();
         EditorGUILayout.LabelField("Light Render Layer Colour Settings", EditorStyles.whiteBoldLabel);
         EditorGUILayout.LabelField($"Configured Light Rendering Layers: {_configuredLightLayers.intValue}");

         EditorGUILayout.PropertyField(_includeInactiveChildren);

         EditorGUILayout.PropertyField(_firstBit);

         var first = Mathf.Clamp(_firstBit.intValue, 1, 31);
         var bitBudget = Mathf.Max(1, 32 - first); // available mask slots starting at firstBit
         var maxUsable = Mathf.Min(bitBudget, _configuredLightLayers.intValue - first);

         _colorCount.intValue = Mathf.Clamp(_colorCount.intValue, 1, maxUsable);
         EditorGUILayout.IntSlider(_colorCount, 1, maxUsable);

         EditorGUILayout.PropertyField(_minSeparation);

         EditorGUILayout.Space();
         EditorGUILayout.LabelField("Light Render Layer Dynamic Settings", EditorStyles.whiteBoldLabel);
         _dynamicMode.boolValue = EditorGUILayout.Toggle("Dynamic Mode", _dynamicMode.boolValue);
         if (_dynamicMode.boolValue)
         {
            EditorGUILayout.PropertyField(_radiusIn);
            EditorGUILayout.PropertyField(_radiusOut);
            EditorGUILayout.PropertyField(_moveGate);
            EditorGUILayout.PropertyField(_coolDownSeconds);
            EditorGUILayout.PropertyField(_tickInterval);
         }

         EditorGUILayout.Space();
         EditorGUILayout.PropertyField(_meshRoomsParent);

         serializedObject.ApplyModifiedProperties();
      }

      private static int GetRenderingLayerCountFromTagManager()
      {
         const string tagManagerPath = "ProjectSettings/TagManager.asset";
         var objs = AssetDatabase.LoadAllAssetsAtPath(tagManagerPath);
         var obj = objs?.FirstOrDefault();
         if (obj == null)
         {
            return 8;
         }

         var so = new SerializedObject(obj);
         var p = so.FindProperty("m_RenderingLayers");
         if (p is {isArray: true})
         {
            return Mathf.Clamp(p.arraySize, 1, 32);
         }

         // Older projects may use different names; try a couple fallbacks
         p = so.FindProperty("renderingLayerNames");
         if (p is {isArray: true})
         {
            return Mathf.Clamp(p.arraySize, 1, 32);
         }

         return 8;
      }
   }
}
