using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using NUnit.Framework.Internal;
using System.Runtime.CompilerServices;
using UnityEditor.AdaptivePerformance.Editor;
using Unity.VisualScripting;


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
    private int LEVEL_COUNT = 25; // Just some overshooting so new levels don't break the array

    public static TelemetryManager Instance { get; private set; }

    public GameObject gameManager;
    private GameManager gameManagerScript;


    //private string filePath;
    //private StreamWriter fileWriter;

    private string sessionID;
    private float levelStartTime = 0.0f;
    private int currLevel = 0;
    private int finalPiecesUsed = 0;
    private List<int> levelAttemptsPreCompletion; // Holds for each level until they beat it
    private List<int> levelAttemptsPostCompletion; // Holds for each level until they get 3 Stars
    private List<bool> levelCompletion;
    private Dictionary<Vector3Int, List<TileType?>> tilePlacements;

    // Can change manager to intercept actions rather than get them passed in at the end

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            gameManagerScript = gameManager.GetComponent<GameManager>();
            sessionID = Guid.NewGuid().ToString();
            levelAttemptsPreCompletion = new List<int>(LEVEL_COUNT);
            levelAttemptsPostCompletion = new List<int>(LEVEL_COUNT);
            levelCompletion = new List<bool>(LEVEL_COUNT);
            tilePlacements = new Dictionary<Vector3Int, List<TileType?>>();
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


    private void OnEnable()

    {
#if !UNITY_EDITOR
        gameManagerScript.ActionDone += eventSub;
#endif
    }


    private void OnDisable()
    {
#if !UNITY_EDITOR
        gameManagerScript.ActionDone -= eventSub;
#endif
    }


    public void StartLevel(int level)
    {
        currLevel = level + 1;
        levelStartTime = Time.realtimeSinceStartup;
    }


    public void eventSub(GameAction gameAction)
    {
        Debug.Log($"GETTING the EVENT");
        if (gameAction.kind == GameActionKind.Undo || gameAction.kind == GameActionKind.TakeBack)
        {
            int board = gameAction.board;
            Vector2Int cellNum = gameAction.square;
            tilePlacements.Remove(new Vector3Int(board, cellNum.x, cellNum.y));
            finalPiecesUsed--;
        }
        else if (gameAction.kind == GameActionKind.PlaceTile)
        {
            int board = gameAction.board;
            Vector2Int cellNum = gameAction.square;
            Vector3Int fullCell = new Vector3Int(board, cellNum.x, cellNum.y);
            if (!tilePlacements.TryGetValue(fullCell, out List<TileType?> tilesHistory))
            {
                tilesHistory = new List<TileType?>();
                tilePlacements[fullCell] = tilesHistory;
            }
            tilePlacements[fullCell].Add(gameAction.tile);
            finalPiecesUsed++;
        }
        else if (gameAction.kind == GameActionKind.LevelEnd)
        {
            LevelSolution currSolution = null;
            // I'm gonna rewrite this to be dynamic but I'm too tired rn
            // so we're hardcoding
            switch (currLevel)
            {
                case 1:
                    currSolution = Solutions.Level1();
                    break;
                case 2:
                    currSolution = Solutions.Level2();
                    break;
                case 3:
                    currSolution = Solutions.Level3();
                    break;
                case 4:
                    currSolution = Solutions.Level4();
                    break;
                case 5:
                    currSolution = Solutions.Level5();
                    break;
                case 6:
                    currSolution = Solutions.Level6();
                    break;
                case 7:
                    currSolution = Solutions.Level7();
                    break;
                case 8:
                    currSolution = Solutions.Level8();
                    break;
                case 9:
                    currSolution = Solutions.Level9();
                    break;
                case 10:
                    currSolution = Solutions.Level10();
                    break;
                case 11:
                    currSolution = Solutions.Level11();
                    break;
                case 12:
                    currSolution = Solutions.Level12();
                    break;
                case 13:
                    currSolution = Solutions.Level13();
                    break;
                case 14:
                    currSolution = Solutions.Level14();
                    break;
                case 15:
                    currSolution = Solutions.Level15();
                    break;
                default:
                    Debug.LogWarning($"TELEMETRY FAILED TO RECONGNIZE THIS LEVEL");
                    return;
            }

            float maxSimilarity = 0.0f;
            foreach (RoadFamily currFamily in currSolution.families)
            {
                Dictionary<Vector3Int, List<TileType?>> solutionHolder = new Dictionary<Vector3Int, List<TileType?>>();
                foreach (AllowedTiles currAllowed in currFamily.squares)
                {
                    int tempBoard = currAllowed.at.board;
                    Vector3Int tempCoords = new Vector3Int(tempBoard, currAllowed.at.square.x, currAllowed.at.square.y);
                    List<TileType?> tempTiles = new List<TileType?>();
                    foreach (TileType tileType in currAllowed.tiles)
                    {
                        tempTiles.Add(tileType);
                    }

                    solutionHolder.Add(tempCoords, tempTiles);

                }

                // Amount that is taken away from the starting similarity of 100 to simulate
                // deviation from the optimal solution
                float startingSimilarity = 100.0f;
                float deviationAmount = startingSimilarity / solutionHolder.Count; // Maybe should change this to be total squares across the boards
                float halfDeviation = deviationAmount / 2.0f;

                foreach (var (cellPos, tiles) in tilePlacements)
                {

                    if (solutionHolder.TryGetValue(cellPos, out List<TileType?> compareTile))
                    {
                        // Cell Pos is correct but what about tile selection
                        foreach (TileType userPlaced in tiles)
                        {
                            if (!compareTile.Contains(userPlaced))
                            {
                                startingSimilarity -= halfDeviation;
                                break;
                            }
                        }
                    }
                    else
                    {
                        // Neither are correct
                        startingSimilarity -= deviationAmount;
                    }

                }

                if (startingSimilarity > maxSimilarity)
                {
                    maxSimilarity = startingSimilarity;
                }
            }

            StartCoroutine(FillOutAnalytics("LevelComplete",
                          sessionID.ToString(),
                          currLevel.ToString(),
                          (Time.realtimeSinceStartup - levelStartTime).ToString("0.00"),
                          finalPiecesUsed.ToString(), gameAction.stars.ToString(), gameAction.score.ToString(),
                          levelAttemptsPreCompletion[currLevel - 1].ToString(),
                          levelAttemptsPostCompletion[currLevel - 1].ToString(),
                          maxSimilarity.ToString()));

            if (currLevel != 0)
            {
                // Fine if sent multiple times over, google sheet will replace duplicates with the one with more attempts
                if (levelCompletion[currLevel - 1])
                {
                    levelAttemptsPostCompletion[currLevel - 1]++;
                    StartCoroutine(FillOutAnalytics("LevelAttemptsPostCompletion",
                              sessionID.ToString(),
                              currLevel.ToString(),
                              (Time.realtimeSinceStartup - levelStartTime).ToString("0.00"),
                              finalPiecesUsed.ToString(), gameAction.stars.ToString(), gameAction.score.ToString(),
                              levelAttemptsPreCompletion[currLevel - 1].ToString(),
                              levelAttemptsPostCompletion[currLevel - 1].ToString(),
                              maxSimilarity.ToString()));
                }
                else
                {
                    if (gameAction.stars > 0)
                    {
                        levelCompletion[currLevel - 1] = true;
                    }
                    levelAttemptsPreCompletion[currLevel - 1]++;
                    StartCoroutine(FillOutAnalytics("LevelAttemptsPreCompletion",
                              sessionID.ToString(),
                              currLevel.ToString(),
                              (Time.realtimeSinceStartup - levelStartTime).ToString("0.00"),
                              finalPiecesUsed.ToString(), gameAction.stars.ToString(), gameAction.score.ToString(),
                              levelAttemptsPreCompletion[currLevel - 1].ToString(),
                              levelAttemptsPostCompletion[currLevel - 1].ToString(),
                              maxSimilarity.ToString()));
                }
            }

            finalPiecesUsed = 0;
            tilePlacements.Clear();
        }
    }


    private IEnumerator FillOutAnalytics(string formType, string mySession, string finalLevel,
                                         string timeElapsed, string totalPieces, string totalStars, string totalScore,
                                         string levelAttemptsPre, string levelAttemptsPost, string similarity)
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
            formData.Add("entry.1315119076", similarity);
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
