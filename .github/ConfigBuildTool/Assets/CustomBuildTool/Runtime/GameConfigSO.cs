using System;
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


        public string GetBaseApiUrl() { return baseApiUrl; }
        public AppServerCode GetAppServerCode() { return appServerCode; }
        public int GetAppVersionCode() { return appVersionCode; }

        public void SetAppVersionCode(int vCode)
        {
            appVersionCode = vCode;
        }

        public string GetConfigProperty(string key)
        {
            return ( from item in configProperties
                where item.key.Equals(key)
                select item).FirstOrDefault().value;
        }

        void OnEnable()
        {
            if(Instance == null)
                Instance = this;
        }
    }

}
