using System.Runtime.InteropServices;
using UnityEngine;

public class WebBrowserBridge : MonoBehaviour
{
    public static WebBrowserBridge Instance { get; private set; }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void ReloadPage();
#endif

    void Awake()
    {
        Instance = this;
    }
    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // 🔥 THIS refreshes the browser tab
    public void ReloadCurrentPage()
    {
        Debug.Log("ReloadCurrentPage CALLED");
#if UNITY_WEBGL && !UNITY_EDITOR
        ReloadPage();
#else
        Debug.LogWarning("ReloadCurrentPage works only in WebGL.");
#endif
    }
}
