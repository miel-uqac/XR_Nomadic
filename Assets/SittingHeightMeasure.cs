using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine;

public class SittingHeightMeasure : MonoBehaviour
{
    public Transform headReference;
    public ScreenHeightAutoAdjust heightAutoAdjust; // ✅ new reference
    private XRGrabInteractable grabInteractable;

    void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        grabInteractable.selectExited.AddListener(OnRelease);
    }

    void OnDestroy()
    {
        grabInteractable.selectExited.RemoveListener(OnRelease);
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        float sittingHeight = transform.position.y;
        Debug.Log("🪑 Estimated Sitting Height: " + sittingHeight.ToString("F2") + " meters");

        // Notify UI system directly
        if (heightAutoAdjust != null)
        {
            heightAutoAdjust.SetSittingHeight(sittingHeight);
        }
        if (headReference != null)
        {
            float relativeHeight = sittingHeight - headReference.position.y;
            Debug.Log("🧠 Relative to Head: " + relativeHeight.ToString("F2") + " meters");
        }
    }
}
