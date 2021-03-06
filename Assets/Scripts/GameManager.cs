﻿using UnityEngine;
using System.Collections.Generic;
using Assets.Scripts;
using System.Linq;
using UnityEngine.SceneManagement;
using System;
using UnityEngine.Events;

public class GameManager : MonoBehaviour
{

    public CameraFollow cameraFollow;
    public GameMenu menu;
    public SlingShot slingshot;
    public static GameState CurrentGameState = GameState.LoadingLevel;

    private int currentBirdIndex;
    private ModulePlayerStates playerStates;
    private List<GameObject> Bricks;
    private List<GameObject> Birds;
    private GameObject Pig;

    void Awake()
    {
        playerStates = GameObject.FindObjectOfType<ModulePlayerStates>();
    }

    // Use this for initialization
    void Start()
    {
        SceneManager.sceneLoaded += OnLoadingLevel;
        SceneManager.sceneUnloaded += OnUnloadingLevel;
        LoadLevel();
    }

    private void LoadLevel()
    {
        if (CurrentGameState == GameState.GameFinished)
            return;

        CurrentGameState = GameState.LoadingLevel;
        SceneManager.LoadScene(2, LoadSceneMode.Additive);
    }

    private void OnLoadingLevel(Scene arg0, LoadSceneMode arg1)
    {
        if (arg0.buildIndex == 2)
        {
            menu.CloseMenu();
            currentBirdIndex = 0;
            //find all relevant game objects
            Bricks = new List<GameObject>(GameObject.FindGameObjectsWithTag("Brick"));
            Birds = new List<GameObject>(GameObject.FindGameObjectsWithTag("Bird"));
            foreach (GameObject go in Birds)
            {
                Bird bird = go.GetComponent<Bird>();
                if (bird == null)
                {
                    Debug.LogError(go.name + "is incorrectly tagged");
                    continue;
                }
                bird.OnHittingSurface += TurnEnded;
            }
            Pig = GameObject.FindGameObjectWithTag("Pig");
            //unsubscribe and resubscribe from the event
            //this ensures that we subscribe only once
            slingshot.BirdThrown -= Slingshot_BirdThrown; slingshot.BirdThrown += Slingshot_BirdThrown;

            CurrentGameState = GameState.Start;
            playerStates.FetchNextTurn(OnReceiveTaskRecommendation);
        }
    }

    private void OnReceiveTaskRecommendation(TaskRecommendation taskRecommendation)
    {
        bool isfreezeStrength = string.Compare(taskRecommendation.TaskName, "STRENGTH") == 0;
        bool isfreezeAngle = string.Compare(taskRecommendation.TaskName, "ANGLE") == 0;
        if (playerStates.PlayerType == 0)
        { 
            slingshot.difficultyLevel = string.Compare(taskRecommendation.TaskName, "TARGET") != 0 ? Mathf.RoundToInt((1 - taskRecommendation.Difficulty) * 20) : 0;
        }
        else
        {
            playerStates.Difficulty = ((float)((float)playerStates.Turns / (float)39));

            slingshot.difficultyLevel = ((playerStates.Turns >= 10 && playerStates.Turns <= 12) || (playerStates.Turns >= 23 && playerStates.Turns <= 25) || (playerStates.Turns >= 36 && playerStates.Turns <= 38))  ? 0 :  Mathf.RoundToInt(20 - (playerStates.Difficulty * 20));            
        }
        
        slingshot.freezeStrength(isfreezeStrength, Pig.transform.position);
        slingshot.freezeAngle(isfreezeAngle, Pig.transform.position);

        GameObject.FindGameObjectWithTag("IndicatorAngle").SetActive(isfreezeAngle);
        GameObject.FindGameObjectWithTag("IndicatorStrength").SetActive(isfreezeStrength);



        slingshot.enabled = false;
        if(this.playerStates.Turns < 39) {
            menu.OpenGameStart();
        }
        else
        {
            CurrentGameState = GameState.GameFinished;

            SceneManager.UnloadSceneAsync(2);
            SceneManager.UnloadSceneAsync(1);
            menu.OpenGameFinished();
        }
    }

    // Update is called once per frame
    void Update()
    {
        switch (CurrentGameState)
        {
            case GameState.LoadingLevel:
                //Do nothing maybe show once a loading screen
                break;
            case GameState.Start:
                //if player taps, begin animating the bird 
                //to the slingshot
                if (Input.GetMouseButtonUp(0))
                {
                    AnimateBirdToSlingshot();
                }
                break;
            case GameState.BirdMovingToSlingshot:
                //do nothing
                break;
            case GameState.Playing:
                //if we have thrown a bird
                //and either everything has stopped moving
                //or there has been 5 seconds since we threw the bird
                //animate the camera to the start position
                if (slingshot.slingshotState == SlingshotState.BirdFlying) /* &&
                    (BricksBirdsPigsStoppedMoving() || Time.time - slingshot.TimeSinceThrown > 5f))*/
                {
                    slingshot.enabled = false;
                    AnimateCameraToStartPosition();
                    CurrentGameState = GameState.BirdMovingToSlingshot;
                }
                break;
            //if we have won or lost, we will restart the level
            //in a normal game, we would show the "Won" screen 
            //and on tap the user would go to the next level
            case GameState.TurnEnded:
                break;
            default:
                break;
            case GameState.GameFinished:
                break;
        }
    }

    private void TurnEnded(Collision2D collision)
    {
        if (collision.gameObject == Pig || currentBirdIndex == Birds.Count - 1)
        {
            CurrentGameState = GameState.TurnEnded;
            SceneManager.UnloadSceneAsync(2);
            //CurrentGameState = GameState.Won;
            //menu.OpenGameWon();
        }
        //animate the next bird, if available
        else
        {
            slingshot.slingshotState = SlingshotState.Idle;
            //bird to throw is the next on the list
            currentBirdIndex++;
            AnimateBirdToSlingshot();
        }

        playerStates.TurnEnded(collision, Pig);
    }

    private void OnUnloadingLevel(Scene arg0)
    {
        if (arg0.buildIndex == 2) {
            LoadLevel();
        }
    }

    /// <summary>
    /// Animates the camera to the original location
    /// When it finishes, it checks if we have lost, won or we have other birds
    /// available to throw
    /// </summary>
    private void AnimateCameraToStartPosition()
    {
        float duration = Vector2.Distance(Camera.main.transform.position, cameraFollow.StartingPosition) / 10f;
        if (duration == 0.0f) duration = 0.1f;
        //animate the camera to start
        Camera.main.transform.positionTo
            (duration,
            cameraFollow.StartingPosition). //end position
            setOnCompleteHandler((x) =>
                        {
                            cameraFollow.IsFollowing = false;
                            
                          
                        });
    }

    /// <summary>
    /// Animates the bird from the waiting position to the slingshot
    /// </summary>
    void AnimateBirdToSlingshot()
    {
        menu.CloseMenu();
        CurrentGameState = GameState.BirdMovingToSlingshot;
        Birds[currentBirdIndex].transform.positionTo
            (Vector2.Distance(Birds[currentBirdIndex].transform.position / 10,
            slingshot.BirdWaitPosition.transform.position) / 10, //duration
            slingshot.BirdWaitPosition.transform.position). //final position
                setOnCompleteHandler((x) =>
                        {
                            x.complete();
                            x.destroy(); //destroy the animation
                            CurrentGameState = GameState.Playing;
                            slingshot.enabled = true; //enable slingshot
                            slingshot.slingshotState = SlingshotState.Idle;
                            //current bird is the current in the list
                            slingshot.BirdToThrow = Birds[currentBirdIndex];
                        });
    }

    /// <summary>
    /// Event handler, when the bird is thrown, camera starts following it
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Slingshot_BirdThrown(object sender, System.EventArgs e)
    {
        cameraFollow.BirdToFollow = Birds[currentBirdIndex].transform;
        cameraFollow.IsFollowing = true;
    }

    /// <summary>
    /// Check if all birds, pigs and bricks have stopped moving
    /// </summary>
    /// <returns></returns>
    bool BricksBirdsPigsStoppedMoving()
    {
        foreach (var item in Bricks.Union(Birds))
        {
            if (item != null && Pig != null && item.GetComponent<Rigidbody2D>().velocity.sqrMagnitude > Constants.MinVelocity && Pig.GetComponent<Rigidbody2D>().velocity.sqrMagnitude > Constants.MinVelocity)
            {
                return false;
            }
        }

        return true;
    }
}