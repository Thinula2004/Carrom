using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_InputField))]
public class ForceUppercaseTMP : MonoBehaviour
{
    private TMP_InputField inputField;

    private void Awake()
    {
        inputField = GetComponent<TMP_InputField>();
        inputField.onValueChanged.AddListener(ForceUppercase);
    }

    private void ForceUppercase(string value)
    {
        string upper = value.ToUpperInvariant();

        if (value == upper)
            return;

        int caretPosition = inputField.caretPosition;

        inputField.SetTextWithoutNotify(upper);
        inputField.caretPosition = Mathf.Min(caretPosition, upper.Length);
        inputField.selectionAnchorPosition = inputField.caretPosition;
        inputField.selectionFocusPosition = inputField.caretPosition;
    }
}