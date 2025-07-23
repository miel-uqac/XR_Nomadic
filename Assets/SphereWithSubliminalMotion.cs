using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class SubliminalUIArcMover : MonoBehaviour
{
    [Header("References")]
    public Transform headTransform;        // To get user gaze and position
    public Transform screenTransform;      // The UI Canvas or screen to move
    [Header("Standing Detection")]
    public float standingHeightThreshold = 1.4f; // Enter standing above this
    [Header("Standing Arc Motion")]
    public float horizontalArcRadius = 0.6f;
    public float horizontalArcAngleDegrees = 20f; // swing range (±10°)
    public float horizontalArcSpeed = 0.25f;      // speed of swing (Hz)
    private Vector3 standingForward; // Cached head forward when entering standing mode
    private Vector3 standingUp;      // Cached head up vector when entering standing mode
    [Header("Movement Settings")]
    public float squareSideLength = 0.5f;  // Side length of the square (e.g., 0.5 meters)
    public float movementSpeed = 0.003f;   // Speed of translation (mm/s)
    public float turnSpeed = 0.25f;        // Speed of turning at the corners
    private float movementTimer = 0f;
    private float standingArcTimer = 0f;
    private float currentYawAngle = 0f; // Horizontal arc angle when standing

    [Header("Standing Position (Stable Workstation)")]
    public Vector3 standingOffset = new Vector3(0, 0f, 0.6f);
    public float keyboardAngle = 25f;
    public float stabilityRadius = 0.5f;
    private Vector3 stableHeadPosition;
    private Vector3 lockedScreenPosition;
    private Quaternion lockedScreenRotation;
    private bool isInStandingMode = false;
    private bool wasStanding = false;
    [Header("XR Rig Height Tracking")]
    public Transform cameraOffsetTransform; // This is the "Camera Offset" object inside your XR Rig
    public float standingDeltaThreshold = 0.25f; // change in height to trigger standing
    private float initialOffsetY;

    [Header("Pivot Settings")]
    public Transform pivotTransform;       // Pivot point to rotate around (e.g. head start pos or empty GameObject)
    public bool useHeadInitialPositionAsPivot = true;

    [Header("Movement Settings")]
    public float maxPitchAngle = 60f;               // Max angle screen moves upward (degrees)
    public float movementSpeedDegreesPerSecond = 0.05f;  // Subliminal movement speed (degrees per second)
    public float transitionSpeed = 5f;               // How fast screen moves to new position

    [Header("Subliminal Rotation Settings")]
    public Vector3 maxRotationOffsets = new Vector3(1f, 0.5f, 0.8f);  // Max pitch, yaw, roll jitter (degrees)
    public Vector3 rotationSpeeds = new Vector3(0.02f, 0.015f, 0.01f); // jitter speeds (degrees/sec)

    private float currentPitchAngle = 0f;
    private Vector3 currentRotationOffset = Vector3.zero;
    private Vector3 rotationDirections = Vector3.one;

    private Vector3 fixedPivotPosition;
    private Quaternion fixedPivotRotation;

    private Vector3 initialScreenPosition;
    private Quaternion initialScreenRotation;
    public float extraSpeedWhenNotLooking = 0.05f; // degrees/sec extra speed when looking away
    public float gazeThresholdDegrees = 15f;      // how close gaze has to be to count as "looking"
    float initialHeadY;
    private bool hasDoneStandingArc = false;
    public Transform arcCenter; // assign a pivot center for the arc (can be user head level)
    public float arcSwingDuration = 1.5f;
    private Vector3 originalScreenPosition;
    private Quaternion originalScreenRotation;

    private Vector3 headInitialPosition;
    private Vector3 headInitialForward;
    private Vector3 headInitialUp;
    void Start()
    {
        // === 1. Set XR Floor Origin ===
        var subsystems = new List<XRInputSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        if (subsystems.Count > 0)
            subsystems[0].TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);

        // === 2. Cache height offset ===
        if (cameraOffsetTransform != null)
            initialOffsetY = cameraOffsetTransform.localPosition.y;

        // === 3. Set pivot point (initial head or assigned) ===
        if (useHeadInitialPositionAsPivot)
        {
            fixedPivotPosition = headTransform.position;
            fixedPivotRotation = headTransform.rotation;
        }
        else if (pivotTransform != null)
        {
            fixedPivotPosition = pivotTransform.position;
            fixedPivotRotation = pivotTransform.rotation;
        }
        else
        {
            Debug.LogError("Set either useHeadInitialPositionAsPivot = true or assign pivotTransform.");
            enabled = false;
            return;
        }

        // === 4. Save original position/rotation of screen and head ===
        originalScreenPosition = screenTransform.position;
        originalScreenRotation = screenTransform.rotation;
        initialScreenPosition = screenTransform.position;
        initialScreenRotation = screenTransform.rotation;
        headInitialPosition = headTransform.position;
        headInitialForward = headTransform.forward;
        headInitialUp = headTransform.up;
        initialHeadY = headTransform.position.y;

        // === 5. Reset arc state ===
        currentPitchAngle = 0f;
        currentRotationOffset = Vector3.zero;
        rotationDirections = new Vector3(
            Random.value > 0.5f ? 1 : -1,
            Random.value > 0.5f ? 1 : -1,
            Random.value > 0.5f ? 1 : -1
        );

        // === 6. Place screen in front of user at head height ===
        Vector3 forward = headTransform.forward;
        Vector3 up = headTransform.up;
        Vector3 startScreenPos = headTransform.position + forward * 0.6f; // 60 cm in front
        Quaternion lookRot = Quaternion.LookRotation(-forward, up);

        screenTransform.position = startScreenPos;
        screenTransform.rotation = lookRot;

        // === 7. Cache this as the true seated reference position ===
        initialScreenPosition = screenTransform.position;
        initialScreenRotation = screenTransform.rotation;
    }

    void Update()
    {
        float headY = headTransform.position.y;
        float deltaY = headY - initialHeadY;
        bool isStanding = deltaY >= 0.25f;

        if (isStanding && !wasStanding)
        {
            EnterStandingMode(headTransform.position, headTransform.forward, headTransform.up);
            wasStanding = true;
        }
        else if (!isStanding && wasStanding)
        {
            ExitStandingMode();
            ResetSittingState(); // reset arc to restart from original seated position
            wasStanding = false;
        }

        if (isInStandingMode)
        {
            UpdateStandingMode();
        }
        else
        {
            UpdateSittingMode(headTransform.position, headTransform.forward, headTransform.up);
        }

    }
    private void ResetSittingState()
    {
        currentPitchAngle = 0f;
        fixedPivotPosition = headInitialPosition;
        fixedPivotRotation = Quaternion.LookRotation(headInitialForward, headInitialUp);

        initialScreenPosition = originalScreenPosition;
        initialScreenRotation = originalScreenRotation;

        screenTransform.position = originalScreenPosition;
        screenTransform.rotation = originalScreenRotation;
    }

    void UpdateSittingMode(Vector3 headPos, Vector3 headForward, Vector3 headUp)
    {
        // #1: Check if the user is currently looking at the screen
        Vector3 dirToScreen = (screenTransform.position - headPos).normalized;
        float angleToScreen = Vector3.Angle(headForward, dirToScreen);
        bool isLooking = angleToScreen < gazeThresholdDegrees;

        // #2: Adjust pitch movement speed based on gaze
        float speed = movementSpeedDegreesPerSecond;
        if (!isLooking)
            speed += extraSpeedWhenNotLooking; // Increase subliminal speed if user is not looking

        // #3: Advance pitch angle upwards (along vertical arc)
        currentPitchAngle += speed * Time.deltaTime;
        currentPitchAngle = Mathf.Clamp(currentPitchAngle, 0f, maxPitchAngle);

        // #4: Compute the vector from pivot to original screen position
        Vector3 vectorFromPivot = initialScreenPosition - fixedPivotPosition;

        // #5: Rotate that vector upward around pivot’s right axis (pitch arc)
        Vector3 pivotRight = fixedPivotRotation * Vector3.right;
        Quaternion pitchRotation = Quaternion.AngleAxis(currentPitchAngle, pivotRight);
        Vector3 rotatedPosition = fixedPivotPosition + pitchRotation * vectorFromPivot;

        // #6: Apply subliminal jitter rotation (pitch/yaw/roll)
        UpdateSubliminalRotations();

        // #7: Determine where the screen should look (toward the pivot) + apply jitter
        Vector3 lookDir = (fixedPivotPosition - rotatedPosition).normalized;
        Vector3 pivotUp = fixedPivotRotation * Vector3.up;

        Quaternion baseRotation = Quaternion.LookRotation(lookDir, pivotUp);
        Quaternion jitterRotation = Quaternion.Euler(currentRotationOffset); // offset in degrees
        Quaternion finalRotation = baseRotation * jitterRotation;

        // #8: Smoothly update the screen's position and rotation
        screenTransform.position = Vector3.Lerp(screenTransform.position, rotatedPosition, transitionSpeed * Time.deltaTime);
        screenTransform.rotation = Quaternion.Slerp(screenTransform.rotation, finalRotation, transitionSpeed * Time.deltaTime);
    }

    // ======= ARC MOVEMENT CODE STARTS HERE =======



    void UpdateSubliminalRotations()
    {
        // #X: Accumulate rotation offsets along each axis, reversing direction when limits are hit
        currentRotationOffset.x += rotationDirections.x * rotationSpeeds.x * Time.deltaTime;
        if (Mathf.Abs(currentRotationOffset.x) > maxRotationOffsets.x)
            rotationDirections.x *= -1f;

        currentRotationOffset.y += rotationDirections.y * rotationSpeeds.y * Time.deltaTime;
        if (Mathf.Abs(currentRotationOffset.y) > maxRotationOffsets.y)
            rotationDirections.y *= -1f;

        currentRotationOffset.z += rotationDirections.z * rotationSpeeds.z * Time.deltaTime;
        if (Mathf.Abs(currentRotationOffset.z) > maxRotationOffsets.z)
            rotationDirections.z *= -1f;
    }

    void EnterStandingMode(Vector3 headPos, Vector3 headForward, Vector3 headUp)
    {
        isInStandingMode = true;

        stableHeadPosition = headPos;
        standingForward = headForward;
        standingUp = headUp;

        if (arcCenter == null)
        {
            Debug.LogWarning("Arc Center is not assigned!");
            return;
        }

        // Place screen at leftmost arc point before animation starts
        Vector3 rightDir = arcCenter.right; // local right direction from arc center
        Vector3 leftDir = -rightDir;
        Vector3 startPos = arcCenter.position + leftDir * horizontalArcRadius;

        screenTransform.position = startPos;
        screenTransform.rotation = Quaternion.LookRotation(stableHeadPosition - startPos, standingUp);

        currentYawAngle = -horizontalArcAngleDegrees * 0.5f; // start from leftmost angle
        
    }

    void UpdateStandingMode()
    {
        if (arcCenter == null) return;

        // Adjust yaw angle using the same logic as vertical pitch
        float speed = movementSpeedDegreesPerSecond;
        currentYawAngle += speed * Time.deltaTime;
        currentYawAngle = Mathf.Clamp(currentYawAngle, -horizontalArcAngleDegrees * 0.5f, horizontalArcAngleDegrees * 0.5f);

        // Vector from arc center to initial screen pos (on horizontal plane)
        Vector3 arcRight = arcCenter.right;
        Quaternion yawRotation = Quaternion.AngleAxis(currentYawAngle, Vector3.up);
        Vector3 offset = yawRotation * arcRight * horizontalArcRadius;
        Vector3 rotatedPosition = arcCenter.position + offset;

        // Look back at the head
        Quaternion lookRotation = Quaternion.LookRotation(stableHeadPosition - rotatedPosition, standingUp);

        // Apply smooth transition
        screenTransform.position = Vector3.Lerp(screenTransform.position, rotatedPosition, transitionSpeed * Time.deltaTime);
        screenTransform.rotation = Quaternion.Slerp(screenTransform.rotation, lookRotation, transitionSpeed * Time.deltaTime);
    }


    void ExitStandingMode()
    {
        isInStandingMode = false;

        screenTransform.position = initialScreenPosition;
        screenTransform.rotation = initialScreenRotation;

        currentPitchAngle = 0f;
        currentYawAngle = 0f;
    }




    void OnDrawGizmos()
    {
        if (headTransform == null || screenTransform == null)
            return;

        // =========================
        // VERTICAL ARC (SITTING)
        // =========================

        if (pivotTransform != null)
        {
            Vector3 pivotPos = pivotTransform.position;
            Quaternion pivotRot = pivotTransform.rotation;
            Vector3 pivotRight = pivotRot * Vector3.right;

            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(pivotPos, 0.05f); // Pivot center

            // Vector from pivot to screen
            Vector3 startVector = screenTransform.position - pivotPos;

            Gizmos.color = Color.magenta;
            int verticalSegments = 30;
            Vector3 prevVerticalPoint = pivotPos + startVector;

            for (int i = 1; i <= verticalSegments; i++)
            {
                float t = i / (float)verticalSegments;
                Quaternion rot = Quaternion.AngleAxis(maxPitchAngle * t, pivotRight);
                Vector3 point = pivotPos + rot * startVector;
                Gizmos.DrawLine(prevVerticalPoint, point);
                Gizmos.DrawSphere(point, 0.01f);
                prevVerticalPoint = point;
            }
        }

        // =========================
        // HORIZONTAL ARC (STANDING)
        // =========================

        if (arcCenter != null)
        {
            Gizmos.color = Color.blue;
            int horizontalSegments = 30;

            Vector3 arcPos = arcCenter.position;
            Vector3 arcRight = arcCenter.right;

            float arcAngle = horizontalArcAngleDegrees;
            float radius = horizontalArcRadius;

            Vector3 prevHorizPoint = arcPos + Quaternion.AngleAxis(-arcAngle * 0.5f, Vector3.up) * arcRight * radius;

            for (int i = 1; i <= horizontalSegments; i++)
            {
                float t = i / (float)horizontalSegments;
                float angle = Mathf.Lerp(-arcAngle * 0.5f, arcAngle * 0.5f, t);

                Quaternion rot = Quaternion.AngleAxis(angle, Vector3.up);
                Vector3 dir = rot * arcRight;
                Vector3 point = arcPos + dir * radius;

                Gizmos.DrawLine(prevHorizPoint, point);
                Gizmos.DrawSphere(point, 0.01f);

                prevHorizPoint = point;
            }

            // Center of arc
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(arcCenter.position, 0.03f);
        }

        // =========================
        // HEAD POSITION DEBUG
        // =========================

        Vector3 headPos = headTransform.position;
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(headPos, 0.05f);
        Gizmos.DrawLine(new Vector3(0, standingHeightThreshold, 0), new Vector3(1, standingHeightThreshold, 0));
    }

}