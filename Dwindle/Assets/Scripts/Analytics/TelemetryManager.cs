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
    [SerializeField]
    private string levelAttemptsPreForm = "https://docs.google.com/forms/d/e/1FAIpQLSfWSrd91_bDOi5Soq1hJyparZxPvLobM7WrLV3ZM9nxDfP8qA/formResponse";
    [SerializeField]
    private string levelAttemptsPostForm = "https://docs.google.com/forms/d/e/1FAIpQLScCDs0U5H07NAn9Es0PmgvXtM5_9z4wHhnYVKrzcyBbRE4Qrg/formResponse";

    [Header("Parameters")]
    [SerializeField]
    private int LEVEL_COUNT = 12;

    public static TelemetryManager Instance { get; private set; }

    //private string filePath;
    //private StreamWriter fileWriter;

    private string sessionID;
    private float levelStartTime = 0.0f;
    private int currLevel = 0;
    private int finalStarLevel = 0;
    private int finalScore = 0;
    private int finalPiecesUsed = 0;
    private List<int> levelAttemptsPreCompletion; // Holds for each level until they beat it
    private List<int> levelAttemptsPostCompletion; // Holds for each level until they get 3 Stars
    private List<bool> levelCompletion;

    // Can change manager to intercept actions rather than get them passed in at the end

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            sessionID = Guid.NewGuid().ToString();
            levelAttemptsPreCompletion = new List<int>(LEVEL_COUNT);
            levelAttemptsPostCompletion = new List<int>(LEVEL_COUNT);
            levelCompletion = new List<bool>(LEVEL_COUNT);
            for (int i = 0; i < LEVEL_COUNT; i++)
            {
                levelAttemptsPreCompletion.Add(0);
                levelAttemptsPostCompletion.Add(0);
                levelCompletion.Add(false);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void StartLevel(int level)
    {
        currLevel = level + 1;
        levelStartTime = Time.realtimeSinceStartup;
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
        // If level is still 0, it was not set meaning a tutorial happened, not a real level
        if (currLevel != 0)
        {
            finalScore = score;
            finalStarLevel = starLevel;
            StartCoroutine(FillOutAnalytics("LevelComplete",
                          sessionID.ToString(),
                          currLevel.ToString(),
                          (Time.realtimeSinceStartup - levelStartTime).ToString("0.00"),
                          finalPiecesUsed.ToString(), finalStarLevel.ToString(), finalScore.ToString(),
                          levelAttemptsPreCompletion[currLevel - 1].ToString(),
                          levelAttemptsPostCompletion[currLevel - 1].ToString()));

            // Fine if sent multiple times over, google sheet will replace duplicates with the one with more attempts
            if (levelCompletion[currLevel - 1])
            {
                levelAttemptsPostCompletion[currLevel - 1]++;
                StartCoroutine(FillOutAnalytics("LevelAttemptsPostCompletion",
                          sessionID.ToString(),
                          currLevel.ToString(),
                          (Time.realtimeSinceStartup - levelStartTime).ToString("0.00"),
                          finalPiecesUsed.ToString(), finalStarLevel.ToString(), finalScore.ToString(),
                          levelAttemptsPreCompletion[currLevel - 1].ToString(),
                          levelAttemptsPostCompletion[currLevel - 1].ToString()));
            }
            else
            {
                if (starLevel > 0)
                {
                    levelCompletion[currLevel - 1] = true;
                }
                levelAttemptsPreCompletion[currLevel - 1]++;
                StartCoroutine(FillOutAnalytics("LevelAttemptsPreCompletion",
                          sessionID.ToString(),
                          currLevel.ToString(),
                          (Time.realtimeSinceStartup - levelStartTime).ToString("0.00"),
                          finalPiecesUsed.ToString(), finalStarLevel.ToString(), finalScore.ToString(),
                          levelAttemptsPreCompletion[currLevel - 1].ToString(),
                          levelAttemptsPostCompletion[currLevel - 1].ToString()));
            }
        }
    }

    private IEnumerator FillOutAnalytics(string formType, string mySession, string finalLevel,
                                         string timeElapsed, string totalPieces, string totalStars, string totalScore,
                                         string levelAttemptsPre, string levelAttemptsPost)
    {
        // Members must be passed in to copy values from start of coroutine so they aren't modified immediately after it's
        // called due to levelStart overwriting their values in the class right after EndLevel is called
        // Only really a problem when level is reset because these calls are on the same frame

        Dictionary<string, string> formData = new Dictionary<string, string>();

        // All questions are listed in order of how they are in
        // their respective Google Form
        string formToSend = "";

        if (formType.Equals("LevelComplete"))
        {
            formToSend = levelCompletionForm;
            formData.Add("entry.825569823", mySession);
            formData.Add("entry.1775572003", finalLevel);
            formData.Add("entry.348468337", timeElapsed);
            formData.Add("entry.1155704640", totalPieces);
            formData.Add("entry.1940427468", totalStars);
            formData.Add("entry.1679653451", totalScore);
        }
        else if (formType.Equals("LevelAttemptsPreCompletion"))
        {
            formToSend = levelAttemptsPreForm;
            formData.Add("entry.795127675", mySession);
            formData.Add("entry.575500443", finalLevel);
            formData.Add("entry.1099898166", levelAttemptsPre);
        }
        else if (formType.Equals("LevelAttemptsPostCompletion"))
        {
            formToSend = levelAttemptsPostForm;
            formData.Add("entry.1181161218", mySession);
            formData.Add("entry.336054205", finalLevel);
            formData.Add("entry.820996616", levelAttemptsPost);
        }

        using (UnityWebRequest webReq = UnityWebRequest.Post(formToSend, formData))
        {
            yield return webReq.SendWebRequest();

            if (webReq.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Telemetry Form Successfully Sent");
            }
            else
            {
                Debug.LogError($"Error when submitting Telemetry Form: {webReq.error}");
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
