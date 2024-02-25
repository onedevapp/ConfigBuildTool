using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OneDevApp.GameConfig
{
    public class GameConfigSelector : MonoBehaviour
    {

#if UNITY_EDITOR
        [SerializeField] private string configSOPathEditor;
#endif

    }



#if UNITY_EDITOR
    [CustomEditor(typeof(GameConfigSelector))]
    public class GameConfigSelectorEditor : Editor
    {
        public string[] options = new string[0];
        private GameConfigSO[] configSOs = new GameConfigSO[0];
        public string[] configSOPaths = new string[0];
        public int index = 0;

        private SerializedProperty configSOPathEditorProperty;

        private void OnEnable()
        {
            configSOPathEditorProperty = serializedObject.FindProperty("configSOPathEditor");

            string[] guids = AssetDatabase.FindAssets("t:GameConfigSO");
            int count = guids.Length;

            options = new string[count];
            configSOPaths = new string[count];
            configSOs = new GameConfigSO[count];
            for (int n = 0; n < count; n++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[n]);
                configSOPaths[n] = path;
                var config = AssetDatabase.LoadAssetAtPath<GameConfigSO>(path);
                configSOs[n] = config;
                options[n] = config.name;
                if (configSOPathEditorProperty.stringValue == configSOPaths[n])
                {
                    index = n;
                }
            }
        }

        //The function that makes the custom editor work
        public override void OnInspectorGUI()
        {

            //base.OnInspectorGUI(); // Draws the default Unity Inspector interface.
            // fetch current values from the target
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            // Display the enum popup in the inspector
            index = EditorGUILayout.Popup("Select GameConfig", index, options);
            if (EditorGUI.EndChangeCheck())
            {
                // Check the value of the enum and display variables based on it
                configSOPathEditorProperty.stringValue = configSOPaths[index];

                // Add the config asset to the PreloadedAssets
                var preloadedAssets = UnityEditor.PlayerSettings.GetPreloadedAssets().ToList();

                for (int i = preloadedAssets.Count() - 1; i >= 0; i--)
                {
                    if (preloadedAssets[i] is GameConfigSO)
                    {
                        preloadedAssets.RemoveAt(i);
                        break;
                    }
                }

                preloadedAssets.Add(configSOs[index]);
                UnityEditor.PlayerSettings.SetPreloadedAssets(preloadedAssets.ToArray());

                // Apply values to the target
                bool modOK = serializedObject.ApplyModifiedProperties();
            }

            // Create a space to separate this enum popup from the other variables 
            EditorGUILayout.Space();
        }
    }
#endif
}
