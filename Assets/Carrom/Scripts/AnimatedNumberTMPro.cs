using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// Animate a TextMeshProUGUI number starting from 0 to a public int "target".
/// - Set the target in the Inspector or change `target` from other scripts at runtime.
/// - Animation will start automatically when `target` changes or when the scene starts (if target != 0).
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class AnimatedNumberTMPro : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI tmpText;

    [Header("Target")]
    [Tooltip("The target integer. Make this public to change from other scripts or the Inspector.")]
    public int target = 0;

    [Header("Animation")]
    [Tooltip("Animation duration in seconds.")]
    public float duration = 1f;
    [Tooltip("Easing curve (0..1 input/output).")]
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Display")]
    [Tooltip("Show integer (true) or two-decimal float (false).")]
    public bool useInteger = true;
    [Tooltip("Use thousands separators for integer display.")]
    public bool useGrouping = true;
    [Tooltip("Optional text prefix (e.g. \"$\")")]
    public string prefix = "";
    [Tooltip("Optional text suffix (e.g. \"+\" or \" pts\")")]
    public string suffix = "";

    [Header("Audio")]
    [Tooltip("Optional AudioSource to play rolling sounds. If left empty, the script will try to GetComponent<AudioSource>().")]
    [SerializeField] private AudioSource sfxSource;
    public AudioClip rollingSound;

    private Coroutine anim;
    private float currentValue = 0f;
    private int lastTarget = int.MinValue; // ensure we detect inspector-set target at runtime

    void Awake()
    {
        if (tmpText == null) tmpText = GetComponent<TextMeshProUGUI>();

        // Fix: assign sfxSource properly or fall back to GetComponent
        if (sfxSource == null)
        {
            sfxSource = GetComponent<AudioSource>();
        }

        if (sfxSource == null)
        {
            // Not fatal: we just won't be able to play sounds
            Debug.LogWarning($"[{nameof(AnimatedNumberTMPro)}] No AudioSource assigned or found on GameObject '{gameObject.name}'. Sound will be disabled.");
        }

        // Always start visible value at 0
        currentValue = 0f;
        UpdateText(currentValue);

        // If the target was set in the Inspector before play, start animating to it
        if (target != 0)
        {
            StartAnimationToTarget(target);
        }

        // record lastTarget so runtime changes are detected afterwards
        lastTarget = target;
    }

    void OnValidate()
    {
        // Keep inspector view up-to-date in edit mode
        if (tmpText == null) tmpText = GetComponent<TextMeshProUGUI>();
        if (!Application.isPlaying)
        {
            // show target in editor, but at runtime Awake will set display to 0
            UpdateText(target);
            lastTarget = target;
        }
    }

    public void PlayRollSound()
    {
        PlaySound(rollingSound);
    }
    public void StopSound()
    {
        if (sfxSource != null) sfxSource.Stop();
    }

    public void PlaySound(AudioClip clip)
    {
        if (clip != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(clip);
        }
    }

    void Update()
    {
        // Detect changes to the public 'target' int and animate when it changes
        if (target != lastTarget)
        {
            StartAnimationToTarget(target);
            lastTarget = target;
        }
    }

    /// <summary>
    /// Convenience method if you prefer calling a method from code instead of setting the public field.
    /// </summary>
    public void SetTarget(int newTarget, float overrideDuration = -1f)
    {
        target = newTarget;
        if (overrideDuration > 0f) duration = overrideDuration;
        // Start animation immediately
        StartAnimationToTarget(newTarget);
        lastTarget = newTarget;
    }

    private void StartAnimationToTarget(int newTarget)
    {
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(AnimateTo(newTarget));
    }

    private IEnumerator AnimateTo(int newTarget)
    {
        float start = currentValue;
        float elapsed = 0f;
        float d = Mathf.Max(0.0001f, duration);

        while (elapsed < d)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / d);
            float eased = ease.Evaluate(t);
            currentValue = Mathf.Lerp(start, newTarget, eased);
            UpdateText(currentValue);
            yield return null;
        }

        currentValue = newTarget;
        UpdateText(currentValue);
        anim = null;
    }

    private void UpdateText(float val)
    {
        if (useInteger)
        {
            int iv = Mathf.RoundToInt(val);
            string s = useGrouping ? iv.ToString("N0") : iv.ToString();
            tmpText.text = prefix + s + suffix;
        }
        else
        {
            tmpText.text = prefix + val.ToString("F2") + suffix;
        }
    }
}