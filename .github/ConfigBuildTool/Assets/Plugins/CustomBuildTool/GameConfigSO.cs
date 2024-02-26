using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OneDevApp.GameConfig
{
    [Serializable]
    public struct AdditionalConfigProperties
    {
        public string key;
        public string value;
    }

    public enum AppServerCode
    {
        D,  //Development
        T,  //Testing
        S,  //Staging
        P,  //Production
    }

    public class GameConfigSO : ScriptableObject
    {
        public static GameConfigSO Instance;

        [SerializeField] private string baseApiUrl;
        [SerializeField] private AppServerCode appServerCode;
        [SerializeField] private int appVersionCode;
        [SerializeField] private AdditionalConfigProperties[] configProperties;

        IReadOnlyDictionary<string, string> configPropertiesDic;

        public string GetBaseApiUrl() { return baseApiUrl; }
        public AppServerCode GetAppServerCode() { return appServerCode; }
        public int GetAppVersionCode() { return appVersionCode; }

        public void SetAppVersionCode(int vCode)
        {
            appVersionCode = vCode;
        }

        public string GetConfigProperty(string key)
        {
            if(configPropertiesDic.ContainsKey(key))
                return configPropertiesDic[key];
            else
                return string.Empty;
        }

        void OnEnable()
        {
            Instance = this;
            configPropertiesDic = configProperties.ToDictionary(item => item.key, item => item.value);
        }
    }

}
