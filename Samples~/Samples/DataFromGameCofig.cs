using System.Text;
using TMPro;
using UnityEngine;
using OneDevApp.GameConfig;
using System.Collections;

public class DataFromGameCofig : MonoBehaviour
{

    public TextMeshProUGUI dataTxt;

    // Start is called before the first frame update
    void Start()
    {
        //yield return new WaitForSeconds(2f);
        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.Append("API URL::").Append(GameConfigSO.Instance.GetBaseApiUrl()).Append("\n");
        stringBuilder.Append("AppServerCode::").Append(GameConfigSO.Instance.GetAppServerCode()).Append("\n");
        stringBuilder.Append("AppVersionCode::").Append(GameConfigSO.Instance.GetAppVersionCode()).Append("\n");
        stringBuilder.Append("FirstKey::").Append(GameConfigSO.Instance.GetConfigProperty("FirstKey")).Append("\n");
        stringBuilder.Append("AnotherKey::").Append(GameConfigSO.Instance.GetConfigProperty("AnotherKey")).Append("\n");
        dataTxt.SetText(stringBuilder.ToString());
    }
}
