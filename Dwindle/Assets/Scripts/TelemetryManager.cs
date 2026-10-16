using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;


// Persistent metric tracker that operates all throughout a
// gameplay session and writes findings to disk

public class TelemetryManager : MonoBehaviour
{
    [Header("Google Form URLS")]
    [SerializeField]
    private string levelCompletionForm = "https://docs.google.com/forms/d/e/1FAIpQLScsOUa-nAnfAcUCfXClZVxhDMw3WuBDLeh9d9UVdUEIBGq4KA/formResponse";


    public static TelemetryManager Instance { get; private set; }

    //private string filePath;
    //private StreamWriter fileWriter;

    private string sessionID;
    private float levelStartTime = 0.0f;
    private int currLevel = 0;
    private int finalStarLevel = 0;
    private int finalScore = 0;
    private int finalPiecesUsed = 0;

    // Can change manager to intercept actions rather than get them passed in at the end

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            sessionID = Guid.NewGuid().ToString();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void StartLevel(int level)
    {
        if (level > 1)
        {
            currLevel = level;
            levelStartTime = Time.realtimeSinceStartup;
        }
    }

    public void AddPiece()
    {
        finalPiecesUsed++;
    }

    public void RemovePiece()
    {
        finalPiecesUsed--;
    }

    public void EndLevel(int score, int starLevel)
    {
        finalScore = score;
        finalStarLevel = starLevel;
        StartCoroutine(FillOutAnalytics("LevelComplete"));
    }

    private IEnumerator FillOutAnalytics(string formType)
    {
        Dictionary<string, string> formData = new Dictionary<string, string>();
        // All questions are listed in order of how they are in
        // their respective Google Form
        if (formType.Equals("LevelComplete"))
        {
            formData.Add("entry.825569823", sessionID);
            formData.Add("entry.1775572003", currLevel.ToString());
            formData.Add("entry.348468337", (Time.realtimeSinceStartup - levelStartTime).ToString("0.00"));
            formData.Add("entry.1155704640", finalPiecesUsed.ToString());
            formData.Add("entry.1940427468", finalStarLevel.ToString());
            formData.Add("entry.1679653451", finalScore.ToString());

            using (UnityWebRequest webReq = UnityWebRequest.Post(levelCompletionForm, formData))
            {
                yield return webReq.SendWebRequest();

                if (webReq.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log("Level Completion Form Successfully Sent");
                }
                else
                {
                    Debug.LogError($"Error when submitting Level Completion Form: {webReq.error}");
                }
            }
        }

    }



    // ONLY USED FOR DOWNLOADABLE BUILDS, NOT WEBGL
    /*
    private void Start()
    {
        if (!Application.isEditor || (Application.isEditor && generateFilesInEditor))
        {
            filePath = Path.Combine(Application.persistentDataPath, "DwindleMetrics.csv");

            fileWriter = new StreamWriter(filePath, true);

            // TODO: See if granularity needed in the way of
            // checking the amount of times specific pieces are used
            fileWriter.WriteLine("Level,TimeElapsed,PiecesUsed,StarLevel,HiScore");
            fileWriter.Flush();
        }
    }
    

    public void LevelStart(int level)
    {
        currLevel = level;
        levelStartTime = Time.timeSinceLevelLoad;
    }

    public void LogCurrentLevel(int piecesUsed, int starAchieved, int score)
    {
        if (fileWriter != null)
        {
            fileWriter.WriteLine($"Level{currLevel},{Time.timeSinceLevelLoad - levelStartTime},{piecesUsed},{starAchieved},{score}");
            fileWriter.Flush();
        }
    }

    public void CleanUp()
    {
        if (fileWriter != null)
        {
            fileWriter.Close();
        }

    }
    */
}
