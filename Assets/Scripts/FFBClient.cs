using UnityEngine;
using Valve.VR;
using Valve.VR.InteractionSystem;

public class FFBClient : MonoBehaviour
{
    private FFBManager _ffbManager;
    private void Awake()
    {
        _ffbManager = GameObject.FindObjectOfType<FFBManager>();
    }

    private void OnHandHoverBegin(Hand hand)
    {
        Debug.Log("Received Hand hover event");
        SteamVR_Skeleton_Pose_Hand skeletonPoseHand;
        if (hand.handType == SteamVR_Input_Sources.LeftHand)
        {
            skeletonPoseHand = GetComponent<Interactable>().skeletonPoser.skeletonMainPose.leftHand;
        }
        else
        {
            skeletonPoseHand = GetComponent<Interactable>().skeletonPoser.skeletonMainPose.rightHand;
        }
        
        _ffbManager.SetForceFeedbackFromSkeleton(hand, skeletonPoseHand);
    }

    private void OnHandHoverEnd(Hand hand)
    {
        if (!hand.currentAttachedObject)
        {
            _ffbManager.RelaxForceFeedback(hand);
        }
    }
}