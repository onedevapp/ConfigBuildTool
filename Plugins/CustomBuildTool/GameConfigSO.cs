using UnityEngine;

namespace OneDevApp.GameConfig
{
    public enum AppServerCode
    {
        D,  //Development
        T,  //Testing
        S,  //Staging
        P,  //Production
    }

    public class GameConfigSO : ScriptableObject
    {

        [SerializeField] private string baseApiUrl;
        [SerializeField] private AppServerCode appServerCode;
        [SerializeField] private int appVersionCode;

        public static GameConfigSO Instance;

        public string GetBaseApiUrl() { return baseApiUrl; }
        public AppServerCode GetAppServerCode() { return appServerCode; }
        public int GetAppVersionCode() { return appVersionCode; }

        public void SetAppVersionCode(int vCode)
        {
            appVersionCode = vCode;
        }

        void OnEnable()
        {
            Instance = this;
        }
    }

}
