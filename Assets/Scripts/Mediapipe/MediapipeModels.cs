using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public enum TypeOfHand
{
    Left,
    Right
}

[Serializable]
public class MpLandmark
{
    public int id;
    /*public float x;
    public float y;
    public float z;*/
    public Vector3 position;
}

[Serializable]
public class MpHand
{
    public string type_of_hand;
    public List<MpLandmark> world_landmarks_list;
    public List<MpLandmark> landmarks_list;

    public MpHand(MpHand mpHand)
    {
        type_of_hand = mpHand.type_of_hand;
        world_landmarks_list = new List<MpLandmark>(mpHand.world_landmarks_list);
        landmarks_list = new List<MpLandmark>(mpHand.landmarks_list);
    }
}

[Serializable]
public class MpData
{
    //public List<mp_landmark> pose_landmarks_list;
    public List<MpHand> hands_list;
    public string image;
    public bool camera_on;
    public int image_width;
    public int image_height;
    public float timestamp_ms;
}

[Serializable]
public class ProcessedLandmark
{
    public int index = -1;
    public Vector3 position = new Vector3();
    public GameObject worldObj = null;
}

[Serializable]
public class ProcessedHand
{
    public bool handVisible;
    public Vector3 center;
    public Vector3 refX;
    public Vector3 refY;
    public Vector3 refZ;
    public float normDistance;
    public TypeOfHand typeOfHand;
    public List<ProcessedLandmark> landmarksList;
    public List<ProcessedLandmark> landmarksListRotated;

    public ProcessedHand()
    {
        handVisible = false;
        landmarksList = new();
        landmarksListRotated = new();
    }

    public ProcessedHand(bool handVisible, Vector3 center, Vector3 refX, Vector3 refY, Vector3 refZ, float normDistance, TypeOfHand typeOfHand, List<ProcessedLandmark> landmarksList, List<ProcessedLandmark> landmarksListRotated)
    {
        this.handVisible = handVisible;
        this.center = center;
        this.refX = refX;
        this.refY = refY;
        this.refZ = refZ;
        this.normDistance = normDistance;
        this.typeOfHand = typeOfHand;
        this.landmarksList = landmarksList;
        this.landmarksListRotated = landmarksListRotated;
    }

    public ProcessedHand(ProcessedHand processedHand)
    {
        handVisible = processedHand.handVisible;
        center = processedHand.center;
        refX = processedHand.refX;
        refY = processedHand.refY;
        refZ = processedHand.refZ;
        normDistance = processedHand.normDistance;
        typeOfHand = processedHand.typeOfHand;

        // Copia profonda delle liste
        landmarksList = new List<ProcessedLandmark>(processedHand.landmarksList.Count);
        foreach (var landmark in processedHand.landmarksList)
        {
            landmarksList.Add(new ProcessedLandmark
            {
                index = landmark.index,
                position = landmark.position,
                worldObj = landmark.worldObj
            });
        }

        landmarksListRotated = new List<ProcessedLandmark>(processedHand.landmarksListRotated.Count);
        foreach (var landmark in processedHand.landmarksListRotated)
        {
            landmarksListRotated.Add(new ProcessedLandmark
            {
                index = landmark.index,
                position = landmark.position,
                worldObj = landmark.worldObj
            });
        }
    }

    public void Clear()
    {
        landmarksList.Clear();
        landmarksListRotated.Clear();
    }
}

//[Serializable]
//public class Processed_body
//{
//    public Vector3 center;
//    public Vector3 ref_x;
//    public Vector3 ref_y;
//    public Vector3 ref_z;
//    public float norm_distance;
//    public List<Vector3> landmarks_list = new List<Vector3>();
//}
