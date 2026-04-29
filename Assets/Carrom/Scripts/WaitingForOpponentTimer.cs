using TMPro;
using UnityEngine;

public class WaitingForOpponentTimer : MonoBehaviour
{
    [SerializeField] private int startTime = 25;

    private int currentTime;
    private float secondCounter;

    TMP_Text timerText;

    void Start()
    {
        timerText = GetComponent<TMP_Text>();
    }

    void OnEnable()
    {
        ResetTimer();
    }

    void Update()
    {
        if (currentTime <= 0)
            return;

        secondCounter += Time.deltaTime;

        if (secondCounter >= 1f)
        {
            currentTime--;
            secondCounter = 0f;

            timerText.text = currentTime.ToString();
        }
    }

    public void ResetTimer()
    {
        currentTime = startTime;
        secondCounter = 0f;
    }
}
