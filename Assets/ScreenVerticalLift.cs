using UnityEngine;

public class ScreenVerticalLift : MonoBehaviour
{
    public float moveSpeed = 1.0f;
    public float targetHeight;
    public float tiltAngle = 15f; // degrees to tilt at the top
    public float tiltSpeed = 30f; // degrees per second
    private bool isMoving = false;
    private bool isTilting = false;
    private Quaternion targetTiltRotation;

    public void MoveToUserHeight(float userHeight)
    {
        targetHeight = userHeight;
        isMoving = true;
        isTilting = false;
        Debug.Log($"Screen mover triggered. Moving from Y={transform.position.y} to Y={targetHeight}");
    }

    void Update()
    {
        if (isMoving)
        {
            Vector3 pos = transform.position;

            if (pos.y < targetHeight)
            {
                float step = moveSpeed * Time.deltaTime;
                pos.y = Mathf.Min(pos.y + step, targetHeight);
                transform.position = pos;
            }
            else
            {
                isMoving = false;
                isTilting = true;

                // Define the tilt rotation (slight backward rotation around X)
                targetTiltRotation = Quaternion.Euler(tiltAngle, transform.rotation.eulerAngles.y, 0f);

                Debug.Log("Screen reached target height. Starting tilt.");
            }
        }

        if (isTilting)
        {
            // Smoothly tilt the screen backward
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetTiltRotation, tiltSpeed * Time.deltaTime);

            if (Quaternion.Angle(transform.rotation, targetTiltRotation) < 0.1f)
            {
                isTilting = false;
                Debug.Log("Tilt complete.");
            }
        }
    }
}
