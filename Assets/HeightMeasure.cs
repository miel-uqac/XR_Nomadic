using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine;

public class HeightMeasure : MonoBehaviour
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
        float height = transform.position.y;
        Debug.Log("Estimated User Height: " + height.ToString("F2") + " meters");

        if (heightAutoAdjust != null)
        {
            heightAutoAdjust.SetStandingHeight(height);
        }

        if (headReference != null)
        {
            float relativeHeight = height - headReference.position.y;
            Debug.Log("Height relative to head reference: " + relativeHeight.ToString("F2") + " meters");
        }
    }
}
