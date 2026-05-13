using System.Runtime.InteropServices;
using UnityEngine;

public class WebBrowserBridge : MonoBehaviour
{
    public static WebBrowserBridge Instance { get; private set; }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void ReloadPage();

    [DllImport("__Internal")]
    private static extern void CopyTextToClipboard(string text);

    [DllImport("__Internal")]
    private static extern void EnterFullscreen();

    [DllImport("__Internal")]
    private static extern void ExitFullscreen();
#endif

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ReloadCurrentPage()
    {
        Debug.Log("ReloadCurrentPage CALLED");

#if UNITY_WEBGL && !UNITY_EDITOR
        ReloadPage();
#else
        Debug.LogWarning("ReloadCurrentPage works only in WebGL.");
#endif
    }

    public void CopyText(string text)
    {
        CopyToClipboard(text);
    }

    public void CopyToClipboard(string text)
    {
        Debug.Log("CopyToClipboard CALLED: " + text);

#if UNITY_WEBGL && !UNITY_EDITOR
        CopyTextToClipboard(text);
#else
        GUIUtility.systemCopyBuffer = text;
#endif
    }

    public void EnterFullScreenMode()
    {
        Debug.Log("EnterFullScreenMode CALLED");

// #if UNITY_WEBGL && !UNITY_EDITOR
//         EnterFullscreen();
// #endif
    }

    public void ExitFullScreenMode()
    {
        Debug.Log("ExitFullScreenMode CALLED");

#if UNITY_WEBGL && !UNITY_EDITOR
        ExitFullscreen();
#endif
    }
}