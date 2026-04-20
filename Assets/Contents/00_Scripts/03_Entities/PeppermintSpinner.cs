using UnityEngine;

public class PeppermintSpinner : MonoBehaviour
{
    public float rotateSpeed = 360f;

    void Update()
    {
        transform.Rotate(0f, 0f, -rotateSpeed * Time.deltaTime);
    }
}