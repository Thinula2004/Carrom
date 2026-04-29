using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SimpleSceneLoader : MonoBehaviour
{
    [Header("Scene Settings")]
    public float loadDelay = 2f;
    
    private AsyncOperation _asyncOperation;
    private bool _isSceneLoaded = false;
    
    void Start()
    {
        // Automatically start loading the Menu scene when this script starts
        StartCoroutine(LoadMenuScene());
    }
    
    [ContextMenu("Load Menu Scene")]
    public void LoadMenu()
    {
        StartCoroutine(LoadMenuScene());
    }
    
    private IEnumerator LoadMenuScene()
    {
        _asyncOperation = SceneManager.LoadSceneAsync("Menu");
        _asyncOperation.allowSceneActivation = false;
        
        while (!_asyncOperation.isDone)
        {
            // Check if scene loading is complete (0.9f means fully loaded but not activated)
            if (_asyncOperation.progress >= 0.9f)
            {
                if (!_isSceneLoaded)
                {
                    _isSceneLoaded = true;
                    // Scene is loaded, now wait for the delay before activating
                    yield return new WaitForSeconds(loadDelay);
                    // Activate the scene
                    _asyncOperation.allowSceneActivation = true;
                }
            }
            
            yield return null;
        }
    }
}