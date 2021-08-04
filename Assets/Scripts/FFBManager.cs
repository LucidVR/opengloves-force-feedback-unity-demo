using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using Valve.VR;
using Valve.VR.InteractionSystem;

public class FFBManager : MonoBehaviour
{
    private Interactable[] _interactables;
    
    private FFBProvider _ffbProviderLeft;
    private FFBProvider _ffbProviderRight;
    
    //Whether to inject the FFBProvider script into all interactable game objects
    public bool injectFfbProvider = true;
    private void Awake()
    {
        _ffbProviderLeft = new FFBProvider(ETrackedControllerRole.LeftHand);
        _ffbProviderRight = new FFBProvider(ETrackedControllerRole.RightHand);
        
        if (injectFfbProvider)
        {
            _interactables = GameObject.FindObjectsOfType<Interactable>();

            foreach (Interactable interactable in _interactables)
            {
                interactable.gameObject.AddComponent<FFBClient>();
            }
        }
        
        Debug.Log("Found: " + _interactables.Length + " Interactables");
    }

    private void _SetForceFeedback(Hand hand, VRFFBInput input)
    {
        if (hand.handType == SteamVR_Input_Sources.LeftHand)
        {
            _ffbProviderLeft.SetFFB(input);
        }
        else
        {
            _ffbProviderRight.SetFFB(input);
        }
    }
    
    //This method (perhaps crudely) estimates the curl of each finger from a skeleton passed in in the skeleton poser.
    //This method is the default option for the FFBClient, which attaches itself to all interactables and calls this method when it receives a hover event.
    public void SetForceFeedbackFromSkeleton(Hand hand, SteamVR_Skeleton_Pose_Hand skeleton)
    {
        SteamVR_Skeleton_Pose_Hand openHand;
        SteamVR_Skeleton_Pose_Hand closedHand;
        
        if (hand.handType == SteamVR_Input_Sources.LeftHand)
        {
            openHand = ((SteamVR_Skeleton_Pose) Resources.Load("ReferencePose_OpenHand")).leftHand;
            closedHand = ((SteamVR_Skeleton_Pose) Resources.Load("ReferencePose_Fist")).leftHand;
        }
        else
        {
            openHand = ((SteamVR_Skeleton_Pose) Resources.Load("ReferencePose_OpenHand")).rightHand;
            closedHand = ((SteamVR_Skeleton_Pose) Resources.Load("ReferencePose_Fist")).rightHand;
        }
        
        List<float>[] fingerCurlValues = new List<float>[5];
        
        for(int i = 0; i < fingerCurlValues.Length; i++) fingerCurlValues[i] = new List<float>();
        
        for (int boneIndex = 0; boneIndex < skeleton.bonePositions.Length; boneIndex++)
        {
            //calculate open hand angle to poser animation
            float openToPoser = Quaternion.Angle(openHand.boneRotations[boneIndex], skeleton.boneRotations[boneIndex]);
            
            //calculate angle from open to closed
            float openToClosed =
                Quaternion.Angle(openHand.boneRotations[boneIndex], closedHand.boneRotations[boneIndex]);
            
            //get the ratio between open to poser and open to closed
            float curl = openToPoser / openToClosed;
            
            //get the finger for the current bone
            int finger = SteamVR_Skeleton_JointIndexes.GetFingerForBone(boneIndex);
            
            if (!float.IsNaN(curl) && curl != 0 && finger >= 0)
            {
                //Add it to the list of bone angles for averaging later
                fingerCurlValues[finger].Add(curl);
            }
        }
        //0-1000 averages of the fingers
        short[] fingerCurlAverages = new short[5];

        for (int i = 0; i < 5; i++)
        {
            float enumerator = 0;
            for (int j = 0; j < fingerCurlValues[i].Count; j++)
            {
                enumerator += fingerCurlValues[i][j];
            }
            
            //The value we to pass is where 0 is full movement flexibility, so invert.
            fingerCurlAverages[i] = Convert.ToInt16(1000 - (Mathf.FloorToInt(enumerator / fingerCurlValues[i].Count * 1000)));
            
            Debug.Log(fingerCurlAverages[i]);
        }
        
        _SetForceFeedback(hand, new VRFFBInput(fingerCurlAverages[0], fingerCurlAverages[1], fingerCurlAverages[2], fingerCurlAverages[3], fingerCurlAverages[4]));
    }

    public void RelaxForceFeedback(Hand hand)
    {
        VRFFBInput input = new VRFFBInput(0, 0, 0, 0, 0);

        _SetForceFeedback(hand, input);
    }

    public void SetForceFeedbackByCurl(Hand hand, VRFFBInput input)
    {
        _SetForceFeedback(hand, input);
    }
    private void Stop()
    {
        _ffbProviderLeft.Close();
        _ffbProviderRight.Close();
    }
    private void OnApplicationQuit()
    {
        Stop();
    }

    private void OnDestroy()
    {
        Stop();
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct VRFFBInput
{
    //Curl goes between 0-1000
    public VRFFBInput(short thumbCurl, short indexCurl, short middleCurl, short ringCurl, short pinkyCurl)
    {
        this.thumbCurl = thumbCurl;
        this.indexCurl = indexCurl;
        this.middleCurl = middleCurl;
        this.ringCurl = ringCurl;
        this.pinkyCurl = pinkyCurl;
    }
    public short thumbCurl;
    public short indexCurl;
    public short middleCurl;
    public short ringCurl;
    public short pinkyCurl;
};

class FFBProvider
{
    private NamedPipesProvider _namedPipeProvider;
    public ETrackedControllerRole controllerRole;
    
    public FFBProvider(ETrackedControllerRole controllerRole)
    {
        this.controllerRole = controllerRole;
        _namedPipeProvider = new NamedPipesProvider(controllerRole);
        
        _namedPipeProvider.Connect();
    }
   
    public bool SetFFB(VRFFBInput input)
    {
         return _namedPipeProvider.Send(input);
    }

    public void Close()
    {
        _namedPipeProvider.Disconnect();
    }
}

class NamedPipesProvider
{
    private NamedPipeClientStream _pipe;
    public NamedPipesProvider(ETrackedControllerRole controllerRole)
    {
        _pipe = new NamedPipeClientStream("vrapplication/ffb/curl/" + (controllerRole == ETrackedControllerRole.RightHand ? "right" : "left"));
    }

    public void Connect()
    {
        try
        {
            Debug.Log("Connecting to pipe");
            _pipe.Connect();
            Debug.Log("Successfully connected to pipe");
        }
        catch (Exception e)
        {
            Debug.Log("Unable to connect to pipe... Assuming that hand is inactive.");
        }
        
    }

    public void Disconnect()
    {
        if (_pipe.IsConnected)
        {
            _pipe.Dispose();
        }
    }

    public bool Send(VRFFBInput input)
    {
        if (_pipe.IsConnected)
        {
            Debug.Log("running task");
            int size = Marshal.SizeOf(input);
            byte[] arr = new byte[size];

            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(input, ptr, true);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);

            _pipe.Write(arr, 0, size);

            Debug.Log("Sent force feedback message.");

            return true;
        }

        return false;
    }
}
