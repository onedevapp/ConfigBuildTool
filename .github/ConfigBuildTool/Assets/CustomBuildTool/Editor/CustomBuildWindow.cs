using UnityEngine;
using UnityEditor;

#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets;
using System.Linq;
using System.IO;
using System.Text;

namespace OneDevApp.GameConfig
{
    public class CustomBuildWindow : EditorWindow
    {
        /**
        * Config constant values and Post Build values
        */
        const string addressableSettingsAsset = "Assets/AddressableAssetsData/AddressableAssetSettings.asset";
        const string configAssetsDefaultPath = "Assets/_GameConfigs";

        const string firebaseDeeplinkFolderPath = "Assets/Firebase/m2repository/com/google/firebase/firebase-dynamic-links-unity";
        const string firebaseMessagingFolderPath = "Assets/Firebase/m2repository/com/google/firebase/firebase-messaging-unity";
        static bool iOSDeeplinkRequired;

#if UNITY_IOS
        static string iOSDeeplinkUrl;
#endif
        static GameConfigSO lastUsedConfigSO;
        static string buildServerCode;
        

        PropertiesLoader buildConfigProperties;
        PropertiesLoader keyStoreProperties;
        AddressableAssetSettings addressableSettings;

        string changesLogFilepath;
        string keyStoreFilePath;
        string buildConfigFilePath;

        string prefixFilePath = string.Empty;
        string finalPath = string.Empty;
        bool showEditPropertiesPanel = false;

        /**
        * Config variables for propertites setup
        */
        string s_GameConfig, s_IOS_DL_Url, k_keyStoreName, k_keyAliasName, k_keyStorePass, k_keyAliasPass;
        int s_Andy_AA_BS, s_IOS_AA_BS, s_Andy_AA_Profile, s_IOS_AA_Profile, s_Andy_Config, s_IOS_Config;
        bool s_choose_BF, s_Andy_SplitBinary, s_IsGame;

        /**
        * Addressables properties
        */
        string[] addressablesProfileOptions = new string[0];
        string[] addressablesBuildScriptOptions = new string[0];
        //string[] addressablesBuildScriptPath = new string[0];
        int buildScriptIndex = 0;
        int profileIndex = 0;

        /**
        * GameConfig properties
        */
        string[] configsOptions = new string[0];
        GameConfigSO[] gameConfigSOs = new GameConfigSO[0];
        int configIndex = 0;

        /**
        * Build specific variables if production then config values used else selected values used
        */
        int[] versionCodesIntArray;
        string appVersion;
        int bundleVersionCode;
        int appMajorVersionCode;
        int appMinorVersionCode;
        int appPatchVersionCode;

        bool isProductionBuild = false;
        bool enableLogs = false;

        string changesLogTxt;

#if UNITY_ANDROID
        bool buildAAB = false;
        bool useKeyStore = false;
#endif

        GameConfigSO selectedConfigSO;

        string addreaasbleBuildScriptPath;
        string adderssableProfileName;

        string prodGameConfigSOPath;

        bool isAllInputValid = true;
        string errorInfoTxt;
        bool editingText;
        Vector2 scrollPos;        


        [MenuItem("Tools/Custom Build Tool")]
        static void Init()
        {
            UnityEditor.EditorWindow window = GetWindow(typeof(CustomBuildWindow));
            window.maxSize = new Vector2(450f, 300f);
            window.minSize = window.maxSize;
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Custom Build Settings");

            appVersion = Application.version;
            changesLogTxt = string.Empty;
            versionCodesIntArray = new int[4];
            string[] versionStringArray = appVersion.Split('.');
            if (versionStringArray.Length > 0)
            {
                appMajorVersionCode = int.Parse(versionStringArray[0]);
                versionCodesIntArray[0] = appMajorVersionCode;
            }
            if (versionStringArray.Length > 1)
            {
                appMinorVersionCode = int.Parse(versionStringArray[1]);
                versionCodesIntArray[1] = appMinorVersionCode;
            }
            if (versionStringArray.Length > 2)
            {
                appPatchVersionCode = int.Parse(versionStringArray[2]);
                versionCodesIntArray[2] = appPatchVersionCode;
            }

#if UNITY_ANDROID
            bundleVersionCode = PlayerSettings.Android.bundleVersionCode;
#elif UNITY_IOS
            bundleVersionCode = int.Parse(PlayerSettings.iOS.buildNumber);
            iOSDeeplinkRequired = AssetDatabase.IsValidFolder(firebaseDeeplinkFolderPath);
#endif
            versionCodesIntArray[3] = bundleVersionCode;

            addressableSettings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(addressableSettingsAsset) as AddressableAssetSettings;
                
            changesLogFilepath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "CHANGELOG.md");
            keyStoreFilePath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "ProjectSettings", "Keystore.properties");
            buildConfigFilePath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "ProjectSettings", "CustomBuildConfig.properties");

            buildConfigProperties = new PropertiesLoader(buildConfigFilePath);
            keyStoreProperties = new PropertiesLoader(keyStoreFilePath);

            ReloadAllConfigs();
            ReloadAddressables();
            
            showEditPropertiesPanel = !buildConfigProperties.IsPropertiesLoaded() || (addressableSettings == null) || (configsOptions.Length <= 0);

            if (showEditPropertiesPanel)
            {
                SetDefaultConfigValues();
                Debug.Log("buildConfigProperties not loaded path::" + buildConfigFilePath);
            }
            
        }

        void OnFocus()
        {
            (isAllInputValid, errorInfoTxt) = validateAllInputs();
        }

        void ReloadAddressables()
        {
            if (addressableSettings == null)
                addressableSettings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(addressableSettingsAsset) as AddressableAssetSettings;

#if UNITY_ANDROID
            prodGameConfigSOPath = buildConfigProperties.get("andy_Prod_GameConfigSO_Path");
#elif UNITY_IOS
            prodGameConfigSOPath = buildConfigProperties.get("ios_Prod_GameConfigSO_Path");
            iOSDeeplinkUrl = buildConfigProperties.get("ios_DeepLink_URL");
#endif

            if(addressableSettings != null)
            {
                addressablesProfileOptions = addressableSettings.profileSettings.GetAllProfileNames().ToArray();

                int buildScriptCount = addressableSettings.DataBuilders.Count;
                addressablesBuildScriptOptions = new string[buildScriptCount];
                for (int n = 0; n < buildScriptCount; n++)
                {
                    addressablesBuildScriptOptions[n] = addressableSettings.DataBuilders[n].name;
                }

            }else
            {                
                Debug.LogError($"{addressableSettingsAsset} couldn't be found or isn't a settings object.");
            }
        }

        void ReloadAllConfigs()
        {
            s_GameConfig = buildConfigProperties.get("gameConfigs_Path", configAssetsDefaultPath);
            if (!AssetDatabase.IsValidFolder(s_GameConfig)) return;

            string[] guidsConfigs = AssetDatabase.FindAssets("t:GameConfigSO", new[] { s_GameConfig });
            int configCount = guidsConfigs.Length;
            gameConfigSOs = new GameConfigSO[configCount];
            configsOptions = new string[configCount];
            for (int n = 0; n < configCount; n++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guidsConfigs[n]);
                gameConfigSOs[n] = AssetDatabase.LoadAssetAtPath<GameConfigSO>(path);
                configsOptions[n] = AssetDatabase.LoadAssetAtPath<GameConfigSO>(path).name;
            }
        }

        void SetDefaultConfigValues()
        {
            s_GameConfig = buildConfigProperties.get("gameConfigs_Path", configAssetsDefaultPath);

            s_Andy_Config = string.IsNullOrEmpty(buildConfigProperties.get("andy_Prod_GameConfigSO_Path")) ? 0 : System.Array.FindIndex(configsOptions, x => x.Equals(buildConfigProperties.get("andy_Prod_GameConfigSO_Path")));            
            s_IOS_Config = string.IsNullOrEmpty(buildConfigProperties.get("ios_Prod_GameConfigSO_Path")) ? 0 : System.Array.FindIndex(configsOptions, x => x.Equals(buildConfigProperties.get("ios_Prod_GameConfigSO_Path")));

            s_Andy_AA_BS = string.IsNullOrEmpty(buildConfigProperties.get("andy_Prod_Addressable_BuildScript")) ? 0 : System.Array.FindIndex(addressablesBuildScriptOptions, x => x.Equals(buildConfigProperties.get("andy_Prod_Addressable_BuildScript")));
            s_IOS_AA_BS = string.IsNullOrEmpty(buildConfigProperties.get("ios_Prod_Addressable_BuildScript")) ? 0 : System.Array.FindIndex(addressablesBuildScriptOptions, x => x.Equals(buildConfigProperties.get("ios_Prod_Addressable_BuildScript")));
            
            s_Andy_AA_Profile = string.IsNullOrEmpty(buildConfigProperties.get("andy_Prod_Adderssable_Profile")) ? 0 : System.Array.FindIndex(addressablesProfileOptions, x => x.Equals(buildConfigProperties.get("andy_Prod_Adderssable_Profile")));
            s_IOS_AA_Profile = string.IsNullOrEmpty(buildConfigProperties.get("ios_Prod_Adderssable_Profile")) ? 0 : System.Array.FindIndex(addressablesProfileOptions, x => x.Equals(buildConfigProperties.get("ios_Prod_Adderssable_Profile")));

            s_IOS_DL_Url = buildConfigProperties.get("ios_DeepLink_URL", "");

            s_choose_BF = buildConfigProperties.get("choose_Build_Folder", "0").Equals("1");
            s_Andy_SplitBinary = buildConfigProperties.get("andy_Prod_Split_Binary", "1").Equals("1");
            s_IsGame = buildConfigProperties.get("andy_IsGame", "1").Equals("1");

            k_keyStoreName = keyStoreProperties.get("keyStorePath", "");
            k_keyAliasName = keyStoreProperties.get("keyAliasName", "");
            k_keyStorePass = keyStoreProperties.get("keyStorePass", "");
            k_keyAliasPass = keyStoreProperties.get("keyAliasPass", "");
            
            iOSDeeplinkRequired = AssetDatabase.IsValidFolder(firebaseDeeplinkFolderPath);
        }

        void CreateNewGameConfig()
        {
            var path = UnityEditor.EditorUtility.SaveFilePanelInProject("Save Config", "config", "asset", string.Empty, s_GameConfig);
            if (!string.IsNullOrEmpty(path))
            {
                var configObject = CreateInstance<GameConfigSO>();
                UnityEditor.AssetDatabase.CreateAsset(configObject, path);
                ReloadAllConfigs();
                if(addressableSettings != null)
                    ReloadAddressables();
            }
        }

        private void OnGUI()
        {
            GUIStyle lableStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft };
            GUIStyle textFieldStyle = new GUIStyle(GUI.skin.textArea) { alignment = TextAnchor.MiddleLeft, wordWrap = false};
            
            EditorGUILayout.BeginVertical();
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            
            if (showEditPropertiesPanel)
            {
                EditorGUILayout.LabelField("Required Production Build Config Values", lableStyle);
                GUILayout.Space(12);
                EditorGUILayout.LabelField("GameConfigs SO Specific Config: ");
                GUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("GameConfigs SO Path: ", GUILayout.Width(150));
                if (!AssetDatabase.IsValidFolder(s_GameConfig) || configsOptions.Length <= 0)
                {
                    EditorGUILayout.LabelField(s_GameConfig, GUILayout.Width(150));
                    //s_GameConfig = EditorGUILayout.TextField(s_GameConfig, textFieldStyle);
                    EditorGUILayout.EndHorizontal();
                    if (GUILayout.Button("Create Config SO"))
                    {
                        if (!Directory.Exists(s_GameConfig))
                            Directory.CreateDirectory(s_GameConfig);

                        CreateNewGameConfig();
                    }
                    EditorGUILayout.EndScrollView();
                    EditorGUILayout.EndVertical();
                    return;
                }               
                else
                {
                    EditorGUILayout.LabelField(s_GameConfig);
                    EditorGUILayout.EndHorizontal();
                }
                
                if(addressableSettings == null)
                {                    
                    GUILayout.Space(8);
                    EditorGUILayout.LabelField("Addressable settings required, Click refresh once created.");
                    if (GUILayout.Button("Refresh"))
                    {                        
                        ReloadAddressables();
                    }

                    EditorGUILayout.EndScrollView();
                    EditorGUILayout.EndVertical();                    
                    return;
                }

                s_Andy_Config = EditorGUILayout.Popup("Android Prod Configs", s_Andy_Config, configsOptions);
                s_IOS_Config = EditorGUILayout.Popup("IOS Prod Configs Path", s_IOS_Config, configsOptions);
                GUILayout.Space(12);

                EditorGUILayout.LabelField("Addressable Specific Config: ");
                GUILayout.Space(4);
                s_Andy_AA_Profile = EditorGUILayout.Popup("Android Prod Profile", s_Andy_AA_Profile, addressablesProfileOptions);
                s_IOS_AA_Profile = EditorGUILayout.Popup("IOS Prod Profile", s_IOS_AA_Profile, addressablesProfileOptions);
                GUILayout.Space(4);
                s_Andy_AA_BS = EditorGUILayout.Popup("Android Prod BuildScript: ", s_Andy_AA_BS, addressablesBuildScriptOptions);
                s_IOS_AA_BS = EditorGUILayout.Popup("IOS Prod BuildScript: ", s_IOS_AA_BS, addressablesBuildScriptOptions);
                GUILayout.Space(8);

                EditorGUILayout.LabelField("Android Specific Config: ");
                GUILayout.Space(4);
                s_Andy_SplitBinary = EditorGUILayout.Toggle("Android Prod SplitBinary: ", s_Andy_SplitBinary);
                s_IsGame = EditorGUILayout.Toggle("Android IsGame: ", s_IsGame);
                GUILayout.Space(8);

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.LabelField("IOS Specific Config: ");
                GUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("IOS DeepLink URL: ", GUILayout.Width(150));
                s_IOS_DL_Url = EditorGUILayout.TextField(s_IOS_DL_Url, textFieldStyle);
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(12);

                s_choose_BF = EditorGUILayout.Toggle("Show Build Folder Dialog: ", s_choose_BF);
                GUILayout.Space(25);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Android KeyStore Config: ");
                
                if (GUILayout.Button("Browse", GUILayout.Width(60)))
                {
                    string path = EditorUtility.OpenFilePanel("Select Existing KeyStore", "", "");
                    if (!string.IsNullOrEmpty(path)){
                        k_keyStoreName = path;
                    }
                }
                
                if (GUILayout.Button("Clear", GUILayout.Width(60)))
                {
                    k_keyStoreName = "";
                    k_keyAliasName = "";
                    k_keyStorePass = "";
                    k_keyAliasPass = "";
                }

                EditorGUILayout.EndHorizontal();
                GUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("KeyStore Path: ", GUILayout.Width(150));
                EditorGUILayout.LabelField(k_keyStoreName);
                EditorGUILayout.EndHorizontal();

                EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(k_keyStoreName));
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("KeyStore Password: ", GUILayout.Width(150));
                k_keyStorePass = EditorGUILayout.TextField(k_keyStorePass, textFieldStyle);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("KeyStore AliasName: ", GUILayout.Width(150));
                k_keyAliasName = EditorGUILayout.TextField(k_keyAliasName, textFieldStyle);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("KeyStore Alias Password: ", GUILayout.Width(150));
                k_keyAliasPass = EditorGUILayout.TextField(k_keyAliasPass, textFieldStyle);
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(8);
                EditorGUI.EndDisabledGroup();

                if (EditorGUI.EndChangeCheck())
                {
                    // the value has changed
                    (isAllInputValid, errorInfoTxt) = validateAllInputs();
                }

                EditorGUILayout.BeginHorizontal();
                    
                EditorGUI.BeginDisabledGroup(!isAllInputValid);
                if (GUILayout.Button("Save to Config Files"))
                {                    
                    buildConfigProperties.set("gameConfigs_Path", s_GameConfig);
                    buildConfigProperties.set("andy_Prod_GameConfigSO_Path", configsOptions[s_Andy_Config]);
                    buildConfigProperties.set("ios_Prod_GameConfigSO_Path", configsOptions[s_IOS_Config]);
                    buildConfigProperties.set("andy_Prod_Addressable_BuildScript", addressablesBuildScriptOptions[s_Andy_AA_BS]);
                    buildConfigProperties.set("ios_Prod_Addressable_BuildScript", addressablesBuildScriptOptions[s_IOS_AA_BS]);
                    buildConfigProperties.set("andy_Prod_Adderssable_Profile", addressablesProfileOptions[s_Andy_AA_Profile]);
                    buildConfigProperties.set("ios_Prod_Adderssable_Profile", addressablesProfileOptions[s_IOS_AA_Profile]);
                    buildConfigProperties.set("choose_Build_Folder", s_choose_BF ? 1 : 0);
                    buildConfigProperties.set("andy_Prod_Split_Binary", s_Andy_SplitBinary ? 1 : 0);
                    buildConfigProperties.set("andy_IsGame", s_IsGame ? 1 : 0);
                    buildConfigProperties.set("ios_DeepLink_URL", s_IOS_DL_Url);

#if UNITY_ANDROID
                    prodGameConfigSOPath = configsOptions[s_Andy_Config];
#elif UNITY_IOS
                    prodGameConfigSOPath = configsOptions[s_IOS_Config];
                    iOSDeeplinkUrl = s_IOS_DL_Url;
#endif

                    buildConfigProperties.Save();
                    
                    keyStoreProperties.set("keyStorePath", k_keyStoreName);
                    keyStoreProperties.set("keyAliasName", k_keyAliasName);
                    keyStoreProperties.set("keyStorePass", k_keyStorePass);
                    keyStoreProperties.set("keyAliasPass", k_keyAliasPass);

                    keyStoreProperties.Save();

                    showEditPropertiesPanel = false;
                }

                if(buildConfigProperties.IsPropertiesLoaded())
                {
                    if (GUILayout.Button("Cancel"))
                    {
                        showEditPropertiesPanel = false;
                        isAllInputValid = true;
                        iOSDeeplinkRequired = false;
                    }
                }
                
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();

                if (!isAllInputValid)
                {
                    EditorGUILayout.HelpBox(errorInfoTxt, MessageType.Error);
                }
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            GUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            // Create GameConfig button
            if (GUILayout.Button("Create GameConfig"))
            {
                var path = UnityEditor.EditorUtility.SaveFilePanelInProject("Save Config", "config", "asset", string.Empty, buildConfigProperties.IsPropertiesLoaded() ? buildConfigProperties.get("gameConfigs_Path") : "");
                if (!string.IsNullOrEmpty(path))
                {
                    var configObject = CreateInstance<GameConfigSO>();
                    UnityEditor.AssetDatabase.CreateAsset(configObject, path);
                    ReloadAllConfigs(); 
                }
            }

            // Edit GameConfig button
            if (GUILayout.Button("Edit Properties"))
            {                
                SetDefaultConfigValues();
                showEditPropertiesPanel = true;
                isAllInputValid = true;
                iOSDeeplinkRequired = false;
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(8);

            // prompt them to save the scene
            if (EditorSceneManager.GetActiveScene().isDirty)
            {
                EditorGUILayout.HelpBox("Save Current scene to take build!", MessageType.Error);
            }
            else
            {
                if (EditorGUIUtility.editingTextField)
                    editingText = true;

                EditorGUILayout.LabelField("Build Setup: ");

                // add your GUI controls to modify the build here
                GUILayout.Space(8);

                EditorGUI.BeginChangeCheck();
                isProductionBuild = EditorGUILayout.Toggle("Release Build: ", isProductionBuild);

                if (isProductionBuild)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Major Version Code: ");
                    GUILayout.Label(appMajorVersionCode.ToString(), lableStyle, GUILayout.Width(30));
                    GUILayout.FlexibleSpace();
                    GUILayout.Space(10);
                    // Disable the Button
                    EditorGUI.BeginDisabledGroup(versionCodesIntArray[0] >= appMajorVersionCode);
                    if (GUILayout.Button("-", EditorStyles.miniButton, GUILayout.Width(30)))
                    {
                        if (appMajorVersionCode > 0)
                            appMajorVersionCode--;

                        appMinorVersionCode = versionCodesIntArray[1];
                        appPatchVersionCode = versionCodesIntArray[2];
                        bundleVersionCode = versionCodesIntArray[3];
                    }
                    EditorGUI.EndDisabledGroup();
                    EditorGUI.BeginDisabledGroup(versionCodesIntArray[0] < appMajorVersionCode);
                    if (GUILayout.Button("+", EditorStyles.miniButton, GUILayout.Width(30)))
                    {
                        appMajorVersionCode++;

                        appMinorVersionCode = 0;
                        appPatchVersionCode = 0;

                        bundleVersionCode = versionCodesIntArray[3] + 1;
                    }
                    EditorGUI.EndDisabledGroup();
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                    GUILayout.Space(8);

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Minor Version Code: ");
                    GUILayout.Label(string.Format("{0:D2}", appMinorVersionCode), lableStyle, GUILayout.Width(30));
                    GUILayout.FlexibleSpace();
                    GUILayout.Space(10);
                    EditorGUI.BeginDisabledGroup(versionCodesIntArray[0] < appMajorVersionCode || versionCodesIntArray[1] >= appMinorVersionCode);
                    if (GUILayout.Button("-", EditorStyles.miniButton, GUILayout.Width(30)))
                    {
                        if (appMinorVersionCode > 0)
                            appMinorVersionCode--;

                        appPatchVersionCode = versionCodesIntArray[2];
                        bundleVersionCode = versionCodesIntArray[3];
                    }
                    EditorGUI.EndDisabledGroup();
                    EditorGUI.BeginDisabledGroup(versionCodesIntArray[0] < appMajorVersionCode || versionCodesIntArray[1] < appMinorVersionCode);
                    if (GUILayout.Button("+", EditorStyles.miniButton, GUILayout.Width(30)))
                    {
                        appMinorVersionCode++;

                        appPatchVersionCode = 0;

                        bundleVersionCode = versionCodesIntArray[3] + 1;
                    }
                    EditorGUI.EndDisabledGroup();
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                    GUILayout.Space(8);

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Patch Version Code: ");
                    GUILayout.Label(string.Format("{0:D2}", appPatchVersionCode), lableStyle, GUILayout.Width(30));
                    GUILayout.FlexibleSpace();
                    GUILayout.Space(10);
                    EditorGUI.BeginDisabledGroup(versionCodesIntArray[0] < appMajorVersionCode || versionCodesIntArray[1] < appMinorVersionCode || versionCodesIntArray[2] >= appPatchVersionCode);
                    if (GUILayout.Button("-", EditorStyles.miniButton, GUILayout.Width(30)))
                    {
                        if (appPatchVersionCode > 0)
                            appPatchVersionCode--;
                        bundleVersionCode = versionCodesIntArray[3];
                    }
                    EditorGUI.EndDisabledGroup();
                    EditorGUI.BeginDisabledGroup(versionCodesIntArray[0] < appMajorVersionCode || versionCodesIntArray[1] < appMinorVersionCode || versionCodesIntArray[2] < appPatchVersionCode);
                    if (GUILayout.Button("+", EditorStyles.miniButton, GUILayout.Width(30)))
                    {
                        appPatchVersionCode++;
                        bundleVersionCode = versionCodesIntArray[3] + 1;
                    }
                    EditorGUI.EndDisabledGroup();
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                    GUILayout.Space(8);

                    EditorGUILayout.BeginHorizontal();
                    //appVersion = EditorGUILayout.TextField("Version: ", appVersion);
                    EditorGUILayout.LabelField("Version Code: ");
                    //GUILayout.Label(System.String.Join(".", versionCodesArray), style, GUILayout.Width(30));
                    appVersion = string.Concat(appMajorVersionCode, ".", string.Format("{0:D2}", appMinorVersionCode), ".", string.Format("{0:D2}", appPatchVersionCode));
                    GUILayout.Label(appVersion, lableStyle, GUILayout.Width(200));
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                    GUILayout.Space(8);

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Bundle Version Code: ");
                    GUILayout.Label(bundleVersionCode.ToString(), lableStyle, GUILayout.Width(30));
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                    GUILayout.Space(8);

                    if (bundleVersionCode > versionCodesIntArray[3])
                    {
                        EditorGUILayout.LabelField("Changes Log: ");
                        changesLogTxt = EditorGUILayout.TextArea(changesLogTxt);
                        GUILayout.Space(8);
                    }

                }

                if (isProductionBuild)
                {
#if UNITY_ANDROID
                    useKeyStore = buildAAB = EditorGUILayout.Toggle("Build ABB: ", buildAAB);
#endif
                }
                else
                {
#if UNITY_ANDROID
                    useKeyStore = EditorGUILayout.Toggle("Use KeyStore: ", useKeyStore);
#endif
                    enableLogs = EditorGUILayout.Toggle("Enable Logs: ", enableLogs);

                    configIndex = EditorGUILayout.Popup("Select GameConfigSO", configIndex, configsOptions);
                    buildScriptIndex = EditorGUILayout.Popup("Select Addressable BuildScript", buildScriptIndex, addressablesBuildScriptOptions);
                    profileIndex = EditorGUILayout.Popup("Select Addressable Profile", profileIndex, addressablesProfileOptions);
                }

#if UNITY_IOS
                if(iOSDeeplinkRequired)
                    iOSDeeplinkUrl = EditorGUILayout.TextField("DeepLink URL: ", buildConfigProperties.get("ios_DeepLink_URL"));            
#endif

                if (EditorGUI.EndChangeCheck())
                {
                    // the value has changed
                    (isAllInputValid, errorInfoTxt) = validateAllInputs();
                }

                if (editingText && !EditorGUIUtility.editingTextField)
                {
                    isAllInputValid = true;
                    editingText = false;

/*#if UNITY_IOS
                    if (iOSDeeplinkRequired && string.IsNullOrEmpty(iOSDeeplinkUrl))
                    {
                        Debug.Log("DeepLinkURL cant be empty!");
                        isAllInputValid = false;
                    }
#endif*/
                }

                GUILayout.Space(8);

                //UIDraw:
                EditorGUI.BeginDisabledGroup(!isAllInputValid);
                // Build button
                //string buildButtonLabel = (buildOptions & BuildOptions.AutoRunPlayer) == 0 ? "Build (with Addressable)" : "Build And Run";
                if (GUILayout.Button("Build (with Addressable)"))
                {

                    PlayerSettings.SplashScreen.showUnityLogo = false;
                    PlayerSettings.bundleVersion = appVersion;

#if UNITY_ANDROID
                    PlayerSettings.Android.bundleVersionCode = bundleVersionCode;

                    EditorUserBuildSettings.buildAppBundle = buildAAB;
                    
                    if (isProductionBuild)
                    {
                        PlayerSettings.Android.useAPKExpansionFiles = buildConfigProperties.get("andy_Prod_Split_Binary").Equals("1"); //Split Application Binary
                        PlayerSettings.Android.androidIsGame = buildConfigProperties.get("andy_IsGame").Equals("1"); //Split Application Binary                        
                    }

                    if (useKeyStore)
                    {
                        if (validateKeyStoreAvailablity())
                        {
                            string fullPath = Path.GetFullPath(keyStoreProperties.get("keyStorePath"));
                            if (File.Exists(fullPath))
                            {

                                PlayerSettings.Android.useCustomKeystore = useKeyStore;

                                PlayerSettings.Android.keystoreName = Path.GetFullPath(keyStoreProperties.get("keyStorePath"));
                                PlayerSettings.Android.keystorePass = keyStoreProperties.get("keyStorePass");
                                PlayerSettings.Android.keyaliasName = keyStoreProperties.get("keyAliasName");
                                PlayerSettings.Android.keyaliasPass = keyStoreProperties.get("keyAliasPass");
                            }
                            else
                            {
                                isAllInputValid = false;
                                errorInfoTxt = "Keystore file not found at path";
                                Debug.LogError("Keystore file not found at path :: " + fullPath);
                                return;
                            }
                        }
                    }
                    else
                    {
                        PlayerSettings.Android.useCustomKeystore = false;
                    }

#elif UNITY_IOS
                    PlayerSettings.iOS.buildNumber = bundleVersionCode.ToString();
#endif
                    if(saveBuildConfig())
                    {
                        // Remove this if you don't want to close the window when starting a build
                        Close();
                        BuildAddressablesAndPlayer(adderssableProfileName, addreaasbleBuildScriptPath);
                    }
                    else
                    {
                        Debug.LogWarning("User cancelled the build");
                    }
                }

                EditorGUI.BeginDisabledGroup(isProductionBuild);
                // Ignore button
                if (GUILayout.Button("Ignore and Build (with Addressable)"))
                {
                    if (saveBuildConfig())
                    {
                        // Remove this if you don't want to close the window when starting a build
                        Close();
                        BuildAddressablesAndPlayer(adderssableProfileName, addreaasbleBuildScriptPath);
                    }
                    else
                    {
                        Debug.LogWarning("User cancelled the build");
                    }
                }

                // Build Only button
                if (GUILayout.Button("Ignore and Build Only"))
                {
                    if (saveBuildConfig())
                    {
                        // Remove this if you don't want to close the window when starting a build
                        Close();
                        BuildPlayerOnly();
                    }
                    else
                    {
                        Debug.LogWarning("User cancelled the build");
                    }
                }
                EditorGUI.EndDisabledGroup();

                // Addressable only button
                if (GUILayout.Button("Addressable Only"))
                {
                    setAddresablesValues();
                    BuildAddressables(adderssableProfileName, addreaasbleBuildScriptPath);
                }
            }
            EditorGUI.EndDisabledGroup();
            if (!isAllInputValid)
            {
                EditorGUILayout.HelpBox(errorInfoTxt, MessageType.Error);
            }
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }


        (bool, string) validateAllInputs()
        {
            if(showEditPropertiesPanel)
            {
                if(!string.IsNullOrEmpty(k_keyStoreName) && !File.Exists(Path.GetFullPath(k_keyStoreName)))
                    return (false, "Keystore file not found at path at "+ Path.GetFullPath(k_keyStoreName));
                else if(!string.IsNullOrEmpty(k_keyStoreName) && (string.IsNullOrEmpty(k_keyAliasName) || string.IsNullOrEmpty(k_keyStorePass) || string.IsNullOrEmpty(k_keyAliasPass)))                
                    return (false, "All keystore values are mandatory");
                else
                    return iOSDeeplinkRequired ? (!string.IsNullOrEmpty(s_IOS_DL_Url), "Deeplink Url cant be empty") : (true, string.Empty);
            }
            else
            {

#if UNITY_ANDROID

                if (isProductionBuild && bundleVersionCode > versionCodesIntArray[3] && string.IsNullOrEmpty(TrimMultiline(changesLogTxt.Trim())))
                {
                    return (false, "Changes logs cant be empty");
                }
                else if (useKeyStore && !validateKeyStoreAvailablity())
                    return (false, "Either Keystore is not enabled or Keystore properties file not found or properties doesnt have respective values");
                else
                    return (true, "");


#elif UNITY_IOS
                if (isProductionBuild && bundleVersionCode > versionCodesIntArray[3] && string.IsNullOrEmpty(TrimMultiline(changesLogTxt.Trim())))
                    return (false, "Changes logs cant be empty");
                else
                    return iOSDeeplinkRequired ? (!string.IsNullOrEmpty(iOSDeeplinkUrl), "deeplinks cant be empty") : (true, string.Empty);
#elif UNITY_STANDALONE_OSX
                return (false, "Unsupported platform");
#elif UNITY_STANDALONE_WIN
                return (false, "Unsupported platform");
#endif
            }
        }

        bool validateKeyStoreAvailablity()
        {
            return
                !string.IsNullOrEmpty(keyStoreProperties.get("keyStorePath")) &&
                    !string.IsNullOrEmpty(keyStoreProperties.get("keyAliasName")) &&
                    !string.IsNullOrEmpty(keyStoreProperties.get("keyStorePass")) &&
                    !string.IsNullOrEmpty(keyStoreProperties.get("keyAliasPass")) && 
                    File.Exists(Path.GetFullPath(keyStoreProperties.get("keyStoreName"))
                );
        }



        bool saveBuildConfig()
        {
            prefixFilePath = string.Empty;
            finalPath = string.Empty;

#if UNITY_ANDROID
            prefixFilePath = string.Concat(Application.productName, "_v", PlayerSettings.Android.bundleVersionCode.ToString(), "_", buildServerCode);
#elif UNITY_IOS
            prefixFilePath = string.Concat(Application.productName, "_v", PlayerSettings.iOS.buildNumber.ToString(), "_", buildServerCode);
#endif

            if (buildConfigProperties.get("choose_Build_Folder").Equals("1"))
            {
#if UNITY_ANDROID
                finalPath = EditorUtility.SaveFilePanel("Choose Location of Built Game", "", prefixFilePath, (isProductionBuild ? ".aab" : ".apk"));
                if(string.IsNullOrEmpty(finalPath)) return false;
#elif UNITY_IOS
                finalPath = EditorUtility.SaveFilePanel("Choose Location of Built Game", "", prefixFilePath, "");
                if(string.IsNullOrEmpty(finalPath)) return false;
#endif
            }

            foreach (var item in gameConfigSOs)
            {
                item.SetAppVersionCode(bundleVersionCode);
                saveData(item);
            }

            if (isProductionBuild)
            {
                selectedConfigSO = AssetDatabase.LoadAssetAtPath<GameConfigSO>(prodGameConfigSOPath);

                PlayerSettings.SplashScreen.showUnityLogo = false;
                UpdateChangesLogTxt(TrimMultiline(changesLogTxt.Trim()));
            }
            else
            {
                selectedConfigSO = gameConfigSOs[configIndex];
            }

            setAddresablesValues();

            buildServerCode = selectedConfigSO.GetAppServerCode().ToString();

            // Add the config asset to the PreloadedAssets
            var preloadedAssets = UnityEditor.PlayerSettings.GetPreloadedAssets().ToList();

            for (int i = preloadedAssets.Count() - 1 ; i >= 0; i--)
            {
                if(preloadedAssets[i] is GameConfigSO)
                {
                    lastUsedConfigSO = preloadedAssets[i] as GameConfigSO;
                    preloadedAssets.RemoveAt(i);
                    break;
                }
            }

            preloadedAssets.Add(selectedConfigSO);
            UnityEditor.PlayerSettings.SetPreloadedAssets(preloadedAssets.ToArray());

            return true;
        }

        void setAddresablesValues()
        {
            
            if (isProductionBuild)
            {

#if UNITY_ANDROID
                adderssableProfileName = buildConfigProperties.get("andy_Prod_Adderssable_Profile");
                addreaasbleBuildScriptPath = buildConfigProperties.get("andy_Prod_Addressable_BuildScript");
#elif UNITY_IOS
                adderssableProfileName = buildConfigProperties.get("ios_Prod_Adderssable_Profile");
                addreaasbleBuildScriptPath = buildConfigProperties.get("ios_Prod_Addressable_BuildScript");
#endif

            }
            else
            {
                adderssableProfileName = addressablesProfileOptions[profileIndex];
                addreaasbleBuildScriptPath = addressablesBuildScriptOptions[buildScriptIndex];
            }
        }

        static void saveData(Object theAsset)
        {
            EditorUtility.SetDirty(theAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [PostProcessBuild]
        public static void OnPostProcessBuild(BuildTarget buildTarget, string pathToBuiltProject)
        {

            if(lastUsedConfigSO != null)
            {
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
                preloadedAssets.Add(lastUsedConfigSO);
                UnityEditor.PlayerSettings.SetPreloadedAssets(preloadedAssets.ToArray());
            }
            

#if UNITY_ANDROID
            PlayerSettings.Android.useCustomKeystore = false;
            EditorUserBuildSettings.buildAppBundle = false;
            PlayerSettings.Android.useAPKExpansionFiles = false; //Split Application Binary

            PlayerSettings.Android.keystoreName = string.Empty;
            PlayerSettings.Android.keystorePass = string.Empty;
            PlayerSettings.Android.keyaliasName = string.Empty;
            PlayerSettings.Android.keyaliasPass = string.Empty;

#elif UNITY_IOS
            if (buildTarget == BuildTarget.iOS && (iOSDeeplinkRequired || AssetDatabase.IsValidFolder(firebaseMessagingFolderPath)))
            {
                AddCapabilities(buildTarget, pathToBuiltProject);
            }
#endif

        }

#if UNITY_IOS
        // This function is used on iOS PostProcessBuild to add AssociatedDomains Entitlement
        private static void AddCapabilities(BuildTarget buildTarget, string pathToBuiltProject)
        {
            // Get ProjectCapabilityManager
            string projectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
            PBXProject project = new PBXProject();
            project.ReadFromString(File.ReadAllText(projectPath));
            string targetGUID = project.GetUnityMainTargetGuid();
            var capabilityManager = new ProjectCapabilityManager(projectPath, "Entitlements.entitlements", null, targetGUID);

            // Add capabilities
            if(AssetDatabase.IsValidFolder(firebaseDeeplinkFolderPath))
                capabilityManager.AddAssociatedDomains(new[] { $"applinks:{iOSDeeplinkUrl}" });

            if (AssetDatabase.IsValidFolder(firebaseMessagingFolderPath))
            {
                capabilityManager.AddPushNotifications(true);
                capabilityManager.AddBackgroundModes(BackgroundModesOptions.RemoteNotifications);
            }

            // Write to file
            capabilityManager.WriteToFile();

            if (AssetDatabase.IsValidFolder(firebaseDeeplinkFolderPath))
            {
                // Get plist
                string plistPath = pathToBuiltProject + "/Info.plist";
                PlistDocument plist = new PlistDocument();
                plist.ReadFromString(File.ReadAllText(plistPath));

                // Get root
                PlistElementDict rootDict = plist.root;

                // Add firebase configs
                rootDict.SetBoolean("FirebaseAppDelegateProxyEnabled", false);
                var customDomains = rootDict.CreateArray("FirebaseDynamicLinksCustomDomains");
                customDomains.AddString($"https://{iOSDeeplinkUrl}");

                // Write to file
                File.WriteAllText(plistPath, plist.WriteToString());
            }
        }
#endif

        bool BuildAddressables(string profile_name, string build_script)
        {
            AddressableAssetSettings.CleanPlayerContent(AddressableAssetSettingsDefaultObject.Settings.ActivePlayerDataBuilder);

            string profileId = addressableSettings.profileSettings.GetProfileId(profile_name);
            if (string.IsNullOrEmpty(profileId))
            {
                Debug.LogWarning($"Couldn't find a profile named, {profile_name}, " +
                                 $"using current profile instead.");
                return false;
            }
            
            addressableSettings.activeProfileId = profileId;

            int index = addressableSettings.DataBuilders.FindIndex(x => x.name.Equals(build_script));
            
            if (index < 0)
            {
                
                Debug.LogWarning($"{build_script} must be added to the " +
                                 $"DataBuilders list before it can be made " +
                                 $"active. Using last run builder instead.");
                return false;
            }
            addressableSettings.ActivePlayerDataBuilderIndex = index;
            
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            bool success = string.IsNullOrEmpty(result.Error);

            if (!success)
            {
                Debug.LogError("Addressables build error encountered: " + result.Error);
            }
            return success;
        }

        void BuildAddressablesAndPlayer(string profile_name, string build_script)
        {
            bool contentBuildSucceeded = BuildAddressables(profile_name, build_script);

            if (contentBuildSucceeded)
            {
                BuildPlayerOnly();
            }
        }

        /// <summary>
        /// Populate the `BuildPlayerOptions` with default values.
        /// </summary>
        public BuildPlayerOptions GetDefaultOptions(BuildTarget target)
        {
            var playerOptions = new BuildPlayerOptions();
            playerOptions.target = target;
            playerOptions.targetGroup = BuildPipeline.GetBuildTargetGroup(target);

            playerOptions.scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            playerOptions.options = BuildOptions.None;


            if (buildConfigProperties.get("choose_Build_Folder").Equals("0"))
            {
                finalPath = Path.Combine(Directory.GetParent(Application.dataPath).Parent.FullName, Application.productName + "_BuildFolder", target.ToString(), prefixFilePath, System.DateTime.Now.ToString("yyyyMMddHHmmss"));

                if (!Directory.Exists(finalPath))
                    Directory.CreateDirectory(finalPath);

#if UNITY_ANDROID
                finalPath = Path.Combine(finalPath, string.Concat(prefixFilePath, isProductionBuild ? ".aab" : ".apk"));
#elif UNITY_IOS
                finalPath = Path.Combine(finalPath, prefixFilePath);
#endif

            }
            else
            {
#if UNITY_ANDROID
                finalPath = string.Concat(finalPath, isProductionBuild ? ".aab" : ".apk");
#endif
            }

            playerOptions.locationPathName = finalPath;

            if(enableLogs)
                playerOptions.extraScriptingDefines = new[] { "ENABLE_LOGS" };

            if (File.Exists(finalPath))
                File.Delete(finalPath);


            playerOptions.options = BuildOptions.CleanBuildCache | BuildOptions.ShowBuiltPlayer;
            return playerOptions;
        }


        void BuildPlayerOnly()
        {
            var buildPlayerOptions = GetDefaultOptions(EditorUserBuildSettings.activeBuildTarget);
            BuildPlayerWindow.DefaultBuildMethods.BuildPlayer(buildPlayerOptions);
        }

        string TrimMultiline(string text)
        {
            return string.Join(System.Environment.NewLine,
                text.Split(new[] { System.Environment.NewLine }, System.StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim()));
        }

        void UpdateChangesLogTxt(string changes)
        {
            if (string.IsNullOrEmpty(changesLogTxt)) return;

            if (!File.Exists(changesLogFilepath))
            {
                using (var sw = File.CreateText(changesLogFilepath))
                {
                    sw.WriteLine("# Versions\n");
                }
            }

            StringBuilder formatTxt = new StringBuilder();
            formatTxt.Append("## v").Append(appVersion).Append(" - ").Append(EditorUserBuildSettings.activeBuildTarget.ToString()).Append(" on ").Append(System.DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss")).Append("\n");
            foreach (string line in changes.Split('\n'))
            {
                formatTxt.Append("* ").Append(line).Append("\n");
            }

            var tempfile = Path.GetTempFileName();
            using (var writer = new StreamWriter(tempfile))
            using (var reader = new StreamReader(changesLogFilepath))
            {
                writer.WriteLine(reader.ReadLine()); // skip one line
                writer.WriteLine(reader.ReadLine()); // skip one line
                writer.WriteLine(formatTxt);

                //writer.WriteLine(changes);
                while (!reader.EndOfStream)
                {
                    writer.WriteLine(reader.ReadLine());
                }
            }
            File.Copy(tempfile, changesLogFilepath, true);
            File.Delete(tempfile);
        }
    }
}
