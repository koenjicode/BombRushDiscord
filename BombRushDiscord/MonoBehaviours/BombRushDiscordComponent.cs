﻿using HarmonyLib;
using Reptile;
using System.Collections.Generic;
using UnityEngine;
using UnityControls = UnityEngine.Windows;
using Logger = BepInEx.Logging.Logger;
using System.Linq;
using BombRushDiscord.Utils;

enum MainState
{
    MAIN_MENU,
    IN_GAME,
}

namespace BombRushDiscord.MonoBehaviours
{
    // TODO Review this file and update to your own requirements, or remove it altogether if not required
    /// <summary>
    /// Template MonoBehaviour class. Use this to add new functionality and behaviours to
    /// the game.
    /// </summary>
    /// 

    internal class BombRushDiscordComponent : MonoBehaviour
    {

        // Discord Information
        public long applicationID = 1144584599406653550;

        public string details;
        public string state;

        public int currentPartySize;
        public int maxPartySize;

        private long elapsedTime;

        private long startTime;
        private long endTime;

        public string smallImage;
        public string smallText;

        public string largeImage;
        public string largeText;

        private Core core;
        private WorldHandler world;

        Dictionary<string, string> gameStages;
        Reptile.Stage currentStage;

        MainState mainState;

        public Discord.Discord mDiscord;

        // BEL, RISE, TRYCE, VINYL
        /// <summary>
        /// Anything that is considered a special name will show a custom icon for the character if icons are enabled.
        /// </summary>
        string[] specialNames = new string[] { "spacegirl", "puffergirl" , "blockguy" , "girl1" };

        /// <summary>
        /// Unity Awake method.
        /// </summary>
        public void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        public bool CanRunDiscordUpdate()
        {
            return core != null & world != null;
        }

        public void UpdateCoreAndWorld()
        {
            core = Core.Instance;
            if (core == null ) { return; }

            world = WorldHandler.instance;
            if ( world == null ) { return; }
        }

        /// <summary>
        /// Unity Start method
        /// </summary>
        public void Start()
        {
            mDiscord = new Discord.Discord(applicationID, (System.UInt64)Discord.CreateFlags.NoRequireDiscord);
        }

        /// <summary>
        /// Unity Awake method. Runs every frame so remove this if not required.
        /// Runs frequently, so remove if not required.
        /// </summary>
        public void Update()
        {

            if (core == null) 
            {
                core = Core.Instance;
            }

            if (world == null)
            {
                world = WorldHandler.instance;
                if( world != null ) 
                {
                    OnGameWorldUpdated();
                    return; 
                }
            }

            DiscordGrabStatusData();

            try
            {
                mDiscord.RunCallbacks();
            }
            catch (System.Exception)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// For things that are not running on full update. For timers etc.
        /// </summary>
        public void OnGameWorldUpdated()
        {
            elapsedTime = System.DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }

        /// <summary>
        /// Unity Physics Update (LateUpdate) method.
        /// Runs frequently, so remove if not required.
        /// </summary>
        public void LateUpdate()
        {
            DiscordUpdateStatus();
        }


        /// <summary>
        /// Updates the information, preparing it to be sent to discord clients.
        /// </summary>
        public void DiscordGrabStatusData()
        {
            // If core is not present we do not even bother with running this.
            if (core == null )
            {
                return;
            }

            // If world is valid we are in a level.
            if (world != null )
            {
                // We use the current objective to retrieve the Chapter.
                currentStage = Utility.GetCurrentStage();
                var currentObjective = Reptile.Story.GetCurrentObjectiveInfo();

                largeImage = "main";

                // If the character has a special name entry we use the appropriate icon for them.
                var character = core.SaveManager.CurrentSaveSlot.currentCharacter;
                var charName = character.ToString().ToLower();

                if (specialNames.Contains(charName))
                {
                    smallImage = charName;
                }
                else
                {
                    smallImage = "fallback";
                }

                smallText = core.Localizer.GetCharacterName(character);
                largeText = core.Localizer.GetStageName(currentStage);

                startTime = elapsedTime;

                // For the state we'll use the level.
                state = string.Format("{0}", core.Localizer.GetStageName(currentStage));

                // Here we update the details based on what we're doing.
                // Are we in a pause menu?
                if (core.IsCorePaused)
                {
                    // Update this with Pause menu Text instead.
                    details = string.Format("{0} {1} : {2}", core.Localizer.GetUserInterfaceText("SAVESLOT_CHAPTER"), (int)currentObjective.chapter, core.Localizer.GetUserInterfaceText("PAUSE_HEADER"));
                }
                // We're not pause but in-game.
                else
                {
                    
                    if (BombRushDiscordPlugin.DisplayMissionObjective.Value)
                    {
                        // Update Chapter Text.
                        // Censor the Mission Objective past Chapter 4.
                        string objectiveText;
                        if ((int)currentObjective.chapter == 4 || (int)currentObjective.chapter == 5)
                        {
                            objectiveText = "???";
                        }
                        else
                        {
                            objectiveText = core.Localizer.GetObjectiveText(currentObjective.id);
                        }

                        details = string.Format("{0} {1} : {2}", core.Localizer.GetUserInterfaceText("SAVESLOT_CHAPTER"), (int)currentObjective.chapter, objectiveText);
                    }
                    else
                    {
                        // If Display Mission Objective is disabled then we just use this.
                        details = string.Format("{0} {1}", core.Localizer.GetUserInterfaceText("SAVESLOT_CHAPTER"), (int)currentObjective.chapter);
                    }
                    
                }

                // For SlopCrew API at a later date.
                if (ModUtils.IsSlopMultiplayer())
                {
                    // currentPartySize = SlopCrew.Plugin.Plugin.PlayerCount;
                    // maxPartySize = 1000;
                }
            }
            else
            {
                details = string.Empty;

                largeImage = "main";
                largeText = "Bomb Rush Cyberfunk";

                smallImage = string.Empty;
                smallText = string.Empty;

                startTime = 0;

                currentPartySize = 0;
                maxPartySize = 0;

                // Is Loading Screen being shown..
                if (core.UIManager.Overlay.loadingScreen.activeSelf)
                {
                    state = string.Format("{0}...", core.Localizer.GetUserInterfaceText("MENU_LOADING"));
                }
                else
                // If Main Menu is showing..
                {
                    state = core.Localizer.GetUserInterfaceText("MAIN_MENU_HEADER");
                }
            }
        }

        /// <summary>
        /// Sends the Discord information to be sent to the client.
        /// </summary>
        public void DiscordUpdateStatus()
        {
            try
            {
                var activityManager = mDiscord.GetActivityManager();
                var activity = new Discord.Activity
                {
                    Details = details,
                    State = state,
                    Party =
                    {
                      Size =
                        {
                            CurrentSize = currentPartySize,
                            MaxSize = maxPartySize,
                        }
                    },
                    Assets =
                    {
                        LargeImage = largeImage,
                        LargeText = largeText,
                        SmallImage = smallImage,
                        SmallText = smallText,
                    },
                    Timestamps =
                    {
                        Start = startTime,
                        End =  endTime,
                    }
                };

                activityManager.UpdateActivity(activity, (res) =>
                {
                    if (res != Discord.Result.Ok) Debug.LogWarning("Failed connecting to Discord!");
                });

            }
            catch (System.Exception)
            {
                throw;
            }
        }
    }
}