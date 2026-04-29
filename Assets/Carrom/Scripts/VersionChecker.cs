using System;
using System.Collections;
using UnityEngine;

public class VersionChecker : MonoBehaviour
{
    public static string version = "2.9";

    public static string GetVersion()
    {
        return "V " + version;
    }

    public static IEnumerator CheckVersion(Action<bool> onResult)
    {
        string remoteVersion = null;

        yield return BR_APIManager.Instance.StartCoroutine(
            BR_APIManager.Instance.FetchGameData(v => remoteVersion = v)
        );

        if (string.IsNullOrEmpty(remoteVersion))
        {
            Debug.LogError("[VersionHandler] Failed to fetch remote version");
            onResult?.Invoke(false);
            yield break;
        }

        Debug.Log($"[VersionHandler] Local version: '{version}'");
        Debug.Log($"[VersionHandler] Remote version: '{remoteVersion}'");

        try
        {
            var localVer = new Version(version.Trim());
            var remoteVer = new Version(remoteVersion.Trim());

            // Compare versions
            int comparison = localVer.CompareTo(remoteVer);

            // TRUE if local >= remote
            bool isValid = comparison >= 0;

            onResult?.Invoke(isValid);
        }
        catch (Exception e)
        {
            Debug.LogError($"[VersionHandler] Version parse error: {e.Message}");
            onResult?.Invoke(false);
        }
    }
}
