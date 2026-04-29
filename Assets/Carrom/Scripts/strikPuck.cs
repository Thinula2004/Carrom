using UnityEngine;

public class strikPuck : MonoBehaviour
{
    [Header("Rotation Settings")]
    [Tooltip("Rotation speed in degrees per second")]
    public float rotationSpeed = 90f;
    
    [Tooltip("Direction of rotation: 1 for clockwise, -1 for counterclockwise")]
    public int rotationDirection = 1;
    
    void Update()
    {
        // Rotate the sprite continuously on the Z-axis
        transform.Rotate(0, 0, rotationSpeed * rotationDirection * Time.deltaTime);
    }
}
