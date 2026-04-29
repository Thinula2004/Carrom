using UnityEngine;

public class BoardSizeManager : MonoBehaviour
{
    [Header("Camera sizes for buckets")]
    public float size16x9 = 4.4f;      // ~1.70–1.85
    public float size185x9 = 5.4f;     // ~1.85–2.10
    public float size195to20x9 = 5.4f; // >2.10

    [Header("Bucket thresholds (by H/W)")]
    [Tooltip("Aspect (H/W) below this is ~16:9 bucket")]
    public float threshold16x9 = 1.85f;
    [Tooltip("Aspect (H/W) below this is ~18.5:9 bucket; above is ~19.5–20:9")]
    public float threshold185x9 = 2.10f;
    void Start()
    {
         var cam = GetComponent<Camera>();
        if (!cam.orthographic)
        {
            Debug.LogWarning("Camera is Perspective; this script expects an Orthographic camera.");
        }

        float aspectHW = (float)Screen.height / Screen.width; // H/W

        if (aspectHW < threshold16x9)
            cam.orthographicSize = size16x9;          // e.g., 1440x2560, 1080x1920 → 4.6
        else if (aspectHW < threshold185x9)
            cam.orthographicSize = size185x9;         // e.g., 1440x2960 → 5.4
        else
            cam.orthographicSize = size195to20x9;     // e.g., 720x1560, 1284x2778 → 5.6
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
