using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.UIElements.Experimental;
using NUnit.Framework;

namespace BEKStudio
{
    public class DemoPlayer
    {
        public string name { get; set; } = "Bot";
        public Sprite profilePic { get; set; }
        public bool isWinning = true;
        public bool isFresh = false;
    }

    public class PhotonController : MonoBehaviourPunCallbacks
    {
        public static PhotonController Instance;

        public string whichMode;
        public string whichRoom;
        public int roomEntryPice = 0;
        public bool playWithBot;
        public int botAvatar;
        public string botName;

        private bool hasJoinedRoom;
        public bool isIntentionalLeave = false;

        // NEW: short reconnect grace loop
        private bool reconnecting = false;
        private const float RejoinTimeoutSeconds = 7f;


        // Reconnect UX config
        private const float LowSignalPingMs = 350f;
        private const float LowSignalVarianceMs = 150f;
        private const float LowSignalHoldSeconds = 2.0f;

        private const float ReconnectPanelTimeoutSeconds = 15f;
        private const float ReconnectAttemptInterval = 0.75f;

        private Coroutine lowSignalCoroutine;
        public Coroutine reconnectCoroutine;

        private bool isLowSignal = false;
        private bool showingReconnectUI = false;

        private const string GAME_ID = "CarromGameV2.2";

        public bool isMissingPlayerMaster = false;


        public float lastSuccessfulPingTime;
        public const float LocalDisconnectThreshold = 1.5f; // 5 seconds without successful ping
        public bool localDisconnectHandled = false;
        public bool offlineMatch = false;
        public DemoPlayer demoPlayer = null;

        private Coroutine joinTimeoutCoroutine;
        private bool matchmakingResolved = false;
        private bool intentionallyLeft = false;
        private bool isCreatingRoom = false;
        bool isFriendRoomFlow = false;
        public Coroutine waitForReconnectionCoroutine = null;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            // Debug.unityLogger.logEnabled = false;
        }

        public void Start()
        {
            Debug.Log("P: Starting");
            isIntentionalLeave = false;
            PhotonNetwork.KeepAliveInBackground = 60;
            lastSuccessfulPingTime = Time.time; // ✅ Initialize
            if (!PhotonNetwork.IsConnectedAndReady)
            {
                PhotonNetwork.ConnectUsingSettings();
            }
            else
            {
                JoinLobby();
            }
        }
        private IEnumerator MonitorLowSignal()
        {
            float badTime = 0f;

            while (true)
            {
                yield return new WaitForSeconds(0.5f);

                // only care during live game
                if (SceneManager.GetActiveScene().name != "Game" || !PhotonNetwork.InRoom)
                {
                    badTime = 0f;
                    isLowSignal = false;
                    continue;
                }

                int ping = PhotonNetwork.GetPing();

                // Better stats (if available)
                var peer = PhotonNetwork.NetworkingClient?.LoadBalancingPeer;
                int rtt = peer != null ? peer.RoundTripTime : ping;
                int varMs = peer != null ? peer.RoundTripTimeVariance : 0;

                bool bad = (rtt >= LowSignalPingMs && varMs >= LowSignalVarianceMs);

                if (bad) badTime += 0.5f;
                else badTime = Mathf.Max(0f, badTime - 0.5f);

                bool nowLow = badTime >= LowSignalHoldSeconds;
                if (nowLow != isLowSignal)
                {
                    isLowSignal = nowLow;

                    // If it recovered and we are only showing UI because of low signal, hide it
                    if (!isLowSignal && showingReconnectUI && PhotonNetwork.IsConnectedAndReady)
                    {
                        if (GameController.Instance != null)
                            GameController.Instance.HideReconnecting();
                        showingReconnectUI = false;
                    }
                }
            }
        }
        public override void OnConnectedToMaster()
        {
            if (PlayerPrefs.HasKey("username"))
            {
                PhotonNetwork.NickName = PlayerPrefs.GetString("username");
            }
            JoinLobby();
        }
        public void CreatePracticeRoom()
        {
            RoomOptions roomOptions = new RoomOptions();
            roomOptions.CustomRoomPropertiesForLobby = new string[] { "roomType" };
            roomOptions.IsOpen = false;
            roomOptions.IsVisible = false;
            // practice room is single-player by design: enforce 1
            roomOptions.MaxPlayers = (byte)1;
            PhotonNetwork.CreateRoom(null, roomOptions);
        }
        public void JoinLobby()
        {
            PhotonNetwork.JoinLobby();
        }

        public override void OnJoinedLobby()
        {
            Debug.Log("joined lobby");
        }


        public void GetBotStatus()
        {
            BR_APIManager.Instance.GetBotState();
        }

        public void FindRoom(BR_APIManager.BotStateData response)
        {
            bool isBotActivated = response.is_carrom_bot_activated;

            matchmakingResolved = false;
            isCreatingRoom = false;
            hasJoinedRoom = false;
            intentionallyLeft = false;
            MenuController.Instance.vsMsgText.text = "Searching room...";


            if (response.is_fresh_carrom_user)
            {
                if (whichRoom == "galle")
                {
                    if (response.current_carrom_sequence != null)
                    {
                        bool value = response.current_carrom_sequence ?? false;
                        MenuController.Instance.VsJoinedRoom();
                        StartCoroutine(FreshUserOfflineMatchCoroutine(value));
                    }
                }
                return;
            }

            // MenuController.Instance.VsJoinedRoom();
            // if (isBotActivated)
            // {
            //     StartCoroutine(FreshUserOfflineMatchCoroutine(false));
            //     return;
            // }

            // if (MenuController.Instance.UMdata != null && MenuController.Instance.UMdata.player_rank == "diamond")
            // {
            //     MenuController.Instance.VsJoinedRoom();
            //     if (isBotActivated)
            //     {
            //         StartCoroutine(FreshUserOfflineMatchCoroutine(false));
            //     }
            //     return;
            // }

            string playerRank = "bronze";
            if (MenuController.Instance.UMdata != null && !string.IsNullOrEmpty(MenuController.Instance.UMdata.player_rank))
            {
                if (MenuController.Instance.UMdata.player_rank == "bronze" || MenuController.Instance.UMdata.player_rank == "silver" || MenuController.Instance.UMdata.player_rank == "gold")
                {
                    playerRank = "bronze";
                }
                else
                {
                    isBotActivated = false;
                    playerRank = "platinum";
                }
                //playerRank = MenuManager.Instance.UMdata.player_rank.ToLower();
            }

            ExitGames.Client.Photon.Hashtable roomHastable = new ExitGames.Client.Photon.Hashtable {
                { "gameId", GAME_ID },
                { "roomType", whichRoom },
                { "isPrivateRoom", false }
            };

            isFriendRoomFlow = false;

            MenuController.Instance.VsJoinedRoom();
            Debug.Log("PR " + playerRank);
            PhotonNetwork.JoinRandomRoom(roomHastable, (byte)2);
            Debug.Log("R: Joining Random");

            if (joinTimeoutCoroutine != null) StopCoroutine(joinTimeoutCoroutine);
            joinTimeoutCoroutine = StartCoroutine(JoinRoomTimeout());

            Debug.Log($"Is it on {isBotActivated}");
            if (isBotActivated)
            {
                StartCoroutine(OfflineMatchCoroutine());
            }
        }

        public void FriendFindRoom(string roomCode)
        {
            if (string.IsNullOrWhiteSpace(roomCode))
            {
                Debug.LogWarning("FriendFindRoom failed: roomCode is empty.");
                return;
            }

            matchmakingResolved = false;
            isCreatingRoom = false;
            hasJoinedRoom = false;
            intentionallyLeft = false;

            isFriendRoomFlow = true;

            MenuController.Instance.vsMsgText.text = "Joining friend room...";
            MenuController.Instance.VsJoinedRoom();

            PhotonNetwork.JoinRoom(roomCode);

            Debug.Log($"Trying to join friend room: {roomCode}");
        }

        public void CreateFriendRoom(string roomCode)
        {
            if (string.IsNullOrWhiteSpace(roomCode))
            {
                Debug.LogWarning("CreateFriendRoom failed: roomCode is empty.");
                return;
            }

            matchmakingResolved = false;
            isCreatingRoom = true;
            hasJoinedRoom = false;
            intentionallyLeft = false;

            isFriendRoomFlow = true;

            MenuController.Instance.vsMsgText.text = "Creating friend room...";
            MenuController.Instance.VsJoinedRoom();

            ExitGames.Client.Photon.Hashtable roomProperties =
                new ExitGames.Client.Photon.Hashtable
                {
                    { "gameId", GAME_ID },
                    { "roomType", whichRoom },
                    { "isPrivateRoom", true },
                    { "roomCode", roomCode }
                };

            RoomOptions roomOptions = new RoomOptions
            {
                MaxPlayers = 2,
                IsVisible = false,
                IsOpen = true,
                CustomRoomProperties = roomProperties,
                CustomRoomPropertiesForLobby = new string[]
                {
                    "gameId",
                    "roomType",
                    "isPrivateRoom",
                    "roomCode"
                }
            };

            PhotonNetwork.CreateRoom(roomCode, roomOptions);

            Debug.Log($"Creating friend room: {roomCode}");
        }

        IEnumerator OfflineMatchCoroutine()
        {
            Debug.Log("R: Offline Match Coroutine Started");
            yield return new WaitForSeconds(Random.Range(20f, 25f));
            Debug.Log("R: Setting Bot");
            if (PhotonNetwork.InRoom)
            {
                if (PhotonNetwork.PlayerList.Length > 1)
                {
                    yield break;
                }
            }

            if (MenuController.Instance.noOpponentFound != null)
            {
                StopCoroutine(MenuController.Instance.noOpponentFound);
            }
            offlineMatch = true;
            Sprite demoSprite = MenuController.Instance.GetRandomDemoProfile();
            demoPlayer = new DemoPlayer
            {
                name = demoSprite.name,
                profilePic = demoSprite
            };

            if (PhotonNetwork.InRoom)
            {
                PhotonNetwork.LeaveRoom();
            }

            StartCoroutine(MenuController.Instance.BotJoinedRoomCoroutine());
        }

        IEnumerator FreshUserOfflineMatchCoroutine(bool currentSequence)
        {
            Debug.Log($"N: Fresh User Offline Coroutine Started. Sequence {currentSequence}");
            yield return new WaitForSeconds(Random.Range(3f, 10f));

            if (MenuController.Instance.noOpponentFound != null)
            {
                StopCoroutine(MenuController.Instance.noOpponentFound);
            }
            offlineMatch = true;
            Sprite demoSprite = MenuController.Instance.GetRandomDemoProfile();
            demoPlayer = new DemoPlayer
            {
                name = demoSprite.name,
                profilePic = demoSprite,
                isWinning = !currentSequence,
                isFresh = true
            };

            if (PhotonNetwork.InRoom)
            {
                PhotonNetwork.LeaveRoom();
            }

            StartCoroutine(MenuController.Instance.BotJoinedRoomCoroutine());
        }

        private IEnumerator JoinRoomTimeout()
        {
            yield return new WaitForSeconds(3f);

            if (matchmakingResolved) yield break;

            if (!PhotonNetwork.InRoom && !hasJoinedRoom)
            {
                Debug.LogWarning("JoinRandomRoom timed out — forcing room creation");
                // ← Don't set matchmakingResolved here
                // Let OnJoinRandomFailed set it, just like the normal path does
                OnJoinRandomFailed(0, "client-side timeout");
            }
        }

        public override void OnJoinRandomFailed(short returnCode, string message)
        {

            if (isFriendRoomFlow)
            {
                isFriendRoomFlow = false;
                Debug.LogWarning($"Failed to join friend room: {message}");
                MenuController.Instance.OnJoinFriendRoomFailed();
                return;
            }

            if (matchmakingResolved) return; // ← Photon firing this after we already handled it

            matchmakingResolved = true;
            isCreatingRoom = true;

            if (joinTimeoutCoroutine != null)
            {
                StopCoroutine(joinTimeoutCoroutine);
                joinTimeoutCoroutine = null;
            }

            string playerRank = "bronze";
            if (MenuController.Instance.UMdata != null && !string.IsNullOrEmpty(MenuController.Instance.UMdata.player_rank))
            {

                if (MenuController.Instance.UMdata.player_rank == "bronze" || MenuController.Instance.UMdata.player_rank == "silver" || MenuController.Instance.UMdata.player_rank == "gold")
                {
                    playerRank = "bronze";
                }
                else
                {
                    playerRank = "platinam";
                }
                //playerRank = MenuManager.Instance.UMdata.player_rank.ToLower();
            }

            ExitGames.Client.Photon.Hashtable roomHastable = new ExitGames.Client.Photon.Hashtable {
            { "gameId", GAME_ID },
            { "roomType", whichRoom },
            { "isPrivateRoom", false }
        };

            RoomOptions roomOptions = new RoomOptions();
            roomOptions.CustomRoomProperties = roomHastable;
            roomOptions.CustomRoomPropertiesForLobby = new string[]
            {
                "gameId",
                "roomType",
                "isPrivateRoom"
            };

            roomOptions.IsOpen = true;
            roomOptions.IsVisible = true;
            roomOptions.MaxPlayers = (byte)2;

            // Keep PlayerTtl for match rooms (grace to rejoin after brief drops)
            roomOptions.PlayerTtl = 15000;  // 30s
            roomOptions.EmptyRoomTtl = 0;
            Debug.Log("PR " + playerRank);
            PhotonNetwork.CreateRoom(null, roomOptions);
        }

        public override void OnJoinRoomFailed(short returnCode, string message)
        {
            Debug.LogWarning($"Friend room join failed. Code: {returnCode}, Message: {message}");

            if (isFriendRoomFlow)
            {
                isFriendRoomFlow = false;
                matchmakingResolved = true;
                isCreatingRoom = false;
                hasJoinedRoom = false;

                Debug.LogWarning($"Friend room join failed: {message}");

                MenuController.Instance.OnJoinFriendRoomFailed();

                return;
            }

            Debug.LogWarning($"JoinRoom failed: {message}");
        }

        public override void OnJoinedRoom()
        {
            // Late JoinRandomRoom response AFTER we already started CreateRoom
            if (!isCreatingRoom && matchmakingResolved)
            {
                Debug.LogWarning("Late JoinRandomRoom response after timeout — leaving stray room");
                intentionallyLeft = true;
                PhotonNetwork.LeaveRoom();
                return;
            }

            isCreatingRoom = false;
            matchmakingResolved = true;

            if (joinTimeoutCoroutine != null)
            {
                StopCoroutine(joinTimeoutCoroutine);
                joinTimeoutCoroutine = null;
            }

            if (isFriendRoomFlow)
            {
                Debug.Log("Joined friend room successfully.");
                isFriendRoomFlow = false;
            }

            MenuController.Instance.HideOpponentPanel();
            if (PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.PlayerCount > PhotonNetwork.CurrentRoom.MaxPlayers)
            {
                Debug.LogWarning($"Joined room over capacity ({PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers}). Leaving and searching again.");
                PhotonNetwork.LeaveRoom();
                MenuController.Instance.vsMsgText.text = "Room full, searching again...";
                BR_APIManager.BotStateData botData = new BR_APIManager.BotStateData
                {
                    is_bot_activated = false,
                    is_carrom_bot_activated = false,
                    current_carrom_sequence = null,
                    is_fresh_carrom_user = false,
                    is_fresh_user = false
                };
                FindRoom(botData);
                return;
            }

            if (whichMode == "practice")
            {
                SceneManager.LoadScene("Practice");
                return;
            }

            ExitGames.Client.Photon.Hashtable userHastable = new ExitGames.Client.Photon.Hashtable();
            userHastable.Add("avatar", PlayerPrefs.GetInt("avatar"));
            userHastable.Add("tag", PhotonNetwork.PlayerList.Length == 1 ? "White" : "Black");
            PhotonNetwork.LocalPlayer.SetCustomProperties(userHastable);

            PhotonNetwork.AutomaticallySyncScene = true;
            MenuController.Instance.VsJoinedRoom();

            if (PhotonNetwork.CurrentRoom.PlayerCount == 2)
            {
                PhotonNetwork.CurrentRoom.IsOpen = false;
                PhotonNetwork.CurrentRoom.IsVisible = false;
                MenuController.Instance.VsOnPlayerJoinedRoom();
            }
            // else
            // {
            //     ExitGames.Client.Photon.Hashtable roomProperties = new ExitGames.Client.Photon.Hashtable();
            //     roomProperties["currentTurnPlayer"] = PhotonNetwork.LocalPlayer.ActorNumber;
            //     PhotonNetwork.CurrentRoom.SetCustomProperties(roomProperties);
            // }

            Debug.Log($"Joined Room: {PhotonNetwork.CurrentRoom.Name} | Players: {PhotonNetwork.CurrentRoom.PlayerCount}");
            hasJoinedRoom = true;

            if (lowSignalCoroutine == null)
                lowSignalCoroutine = StartCoroutine(MonitorLowSignal());
        }

        public override void OnLeftRoom()
        {
            if (offlineMatch || intentionallyLeft || isIntentionalLeave)
            {
                intentionallyLeft = false;
                return;
            }

            PhotonNetwork.AutomaticallySyncScene = false;

            if (SceneManager.GetActiveScene().name == "Menu")
            {
                MenuController.Instance.VsOnLeftRoom();
            }

            // if(!showingReconnectUI && !isIntentionalLeave)
            // {
            //     GameController.Instance.ShowDisconnectLose();
            // }
        }

        public void MarkIntentionalLeave()
        {
            isIntentionalLeave = true;
        }

        public override void OnCreatedRoom()
        {
            Debug.Log("Friend room created successfully: " + PhotonNetwork.CurrentRoom.Name);
        }

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                StartCoroutine(OnPlayerEnteredRoomCoroutine());
            }
        }


        public override void OnCreateRoomFailed(short returnCode, string message)
        {
            Debug.LogWarning($"Friend room create failed. Code: {returnCode}, Message: {message}");
        }

        public IEnumerator OnPlayerEnteredRoomCoroutine()
        {
            yield return new WaitForSeconds(3f);

            if (PhotonNetwork.CurrentRoom == null) yield break;

            if (PhotonNetwork.CurrentRoom.PlayerCount == 2)
            {
                Debug.Log("Both players in room - starting game");
                PhotonNetwork.CurrentRoom.IsOpen = false;
                PhotonNetwork.CurrentRoom.IsVisible = false;
                MenuController.Instance.VsOnPlayerJoinedRoom();
            }
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            if (SceneManager.GetActiveScene().name == "Game")
            {
                return;
            }
        }

        public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable changedProps)
        {
            Debug.Log("Room properties updated: " + string.Join(", ", changedProps.Keys));
            if (changedProps.ContainsKey("playerMissing"))
            {
                object value = changedProps["playerMissing"];
                if (value is int missingId)
                {
                    if (PhotonNetwork.LocalPlayer.ActorNumber != missingId)
                    {
                        if (!GameController.Instance.isWaitingForReconnection)
                        {
                            GameController.Instance.isWaitingForReconnection = true;
                            GameController.Instance.ShowWaitingForOpponent();
                            if (waitForReconnectionCoroutine != null)
                            {
                                StopCoroutine(waitForReconnectionCoroutine);
                                waitForReconnectionCoroutine = null;
                            }
                            waitForReconnectionCoroutine = StartCoroutine(GameController.Instance.WaitForReconnection());
                        }
                    }
                }
                else
                {
                    GameController.Instance.allowPocketing = true;
                    GameController.Instance.reduceTimer = true;
                    GameController.Instance.HideWaitingForOpponent();
                    if (waitForReconnectionCoroutine != null)
                    {
                        StopCoroutine(waitForReconnectionCoroutine);
                        waitForReconnectionCoroutine = null;
                    }
                    GameController.Instance.isWaitingForReconnection = false;
                }
            }
        }

        public void CheckPing()
        {
            foreach (Player player in PhotonNetwork.PlayerList)
            {
                if (player == PhotonNetwork.LocalPlayer) continue;

                if (player.CustomProperties.TryGetValue("lastPing", out object pingObj))
                {
                    int lastPing = (int)pingObj;
                    int currentTime = PhotonNetwork.ServerTimestamp;

                    if (currentTime - lastPing > 2000)
                    {
                        Debug.LogWarning($"D: Player {player.ActorNumber} appears to be disconnected.");
                        if (PhotonNetwork.IsMasterClient)
                        {
                            NotifyPlayerMissing(player.ActorNumber);
                            return;
                        }
                        // only handle one missing player at a time
                    }
                    else
                    {
                        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("playerMissing", out object missingPlayerObj) &&
                            missingPlayerObj is int missingId &&
                            missingId == player.ActorNumber)
                        {
                            Debug.Log($"D: Player {player.ActorNumber} reconnected — clearing wait overlay.");

                            ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
                            props["playerMissing"] = null;
                            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
                        }
                    }
                }
                else
                {
                    ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
                    props["lastPing"] = PhotonNetwork.ServerTimestamp;
                    player.SetCustomProperties(props);
                }
            }
        }

        void NotifyPlayerMissing(int actorNumber)
        {
            ExitGames.Client.Photon.Hashtable roomProps = new ExitGames.Client.Photon.Hashtable();
            roomProps["playerMissing"] = actorNumber;
            PhotonNetwork.CurrentRoom.SetCustomProperties(roomProps);
            Debug.Log("D: Notifying player missing " + actorNumber);
        }

        //public override void OnMasterClientSwitched(Player newMasterClient)
        //{
        //    GameController.Instance.masterClientTag = (string)newMasterClient.CustomProperties["tag"];
        //    if (GameController.Instance.whichPlayer == GameController.WhichPlayer.ME)
        //    {
        //        GameController.Instance.whichPlayer = GameController.WhichPlayer.OTHER;
        //    }
        //    else
        //    {
        //        GameController.Instance.whichPlayer = GameController.WhichPlayer.ME;
        //    }
        //    GameController.Instance.CheckTurn();
        //}

        public void HandleLocalDisconnect()
        {
            if (localDisconnectHandled) return;
            localDisconnectHandled = true;

            Debug.Log("[LocalDisconnect] Detected local connection loss - starting recovery");

            if (SceneManager.GetActiveScene().name == "Game")
            {
                if (reconnectCoroutine == null)
                {
                    reconnectCoroutine = StartCoroutine(ReconnectFlow());
                }
            }
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            Debug.Log($"Disconnected: {cause}");

            if (offlineMatch)
            {
                return;
            }

            // ignore intentional leave
            // if (isIntentionalLeave)
            // {
            //     isIntentionalLeave = false;
            //     return;
            // }

            // Only run in Game scene
            if (SceneManager.GetActiveScene().name != "Game123")
                return;

            // Start reconnect attempts (single instance)
            if (reconnectCoroutine == null)
                reconnectCoroutine = StartCoroutine(ReconnectFlow());
        }

        public IEnumerator ReconnectFlow()
        {
            localDisconnectHandled = true;

            GameController.Instance.HideDiskAndPucks();
            GameController.Instance.allowPocketing = false;

            if (GameController.Instance.whichPlayer == GameController.WhichPlayer.ME)
            {
                PlayerPuck.Instance.DisableCollider();
            }

            float elapsed = 0f;
            bool reconnectUiWasShown = false;

            // ✅ Always show reconnect UI
            if (GameController.Instance != null)
            {
                GameController.Instance.ShowReconnecting();
                PlayerPuck.Instance.DisableTouchControls();
                showingReconnectUI = true;
                reconnectUiWasShown = true;
            }

            // ✅ Save room name for rejoin attempts
            string savedRoomName = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.Name : null;

            float timeSinceLastAttempt = 0f;
            const float minTimeBetweenAttempts = 1.5f;

            while (elapsed < ReconnectPanelTimeoutSeconds)
            {
                // ✅ SUCCESS - Back in the room!
                if (PhotonNetwork.IsConnectedAndReady && PhotonNetwork.InRoom)
                {
                    Debug.Log("✅ [Reconnect] Successfully back in room!");

                    if (GameController.Instance != null)
                    {
                        GameController.Instance.HideReconnecting();
                        GameController.Instance.ShowDiskAndPucks();
                    }

                    PlayerPuck.Instance.EnableTouchControls();
                    showingReconnectUI = false;
                    reconnectCoroutine = null;
                    localDisconnectHandled = false;

                    // Sync turn state
                    if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("currentTurnPlayer", out object value))
                    {
                        Debug.Log("Value = " + value + " Master = " + PhotonNetwork.MasterClient.ActorNumber);
                        if (PhotonNetwork.MasterClient.ActorNumber != (int)value)
                        {
                            if (GameController.Instance.AllLoaded())
                            {
                                if (GameController.Instance.redPuckWaiting)
                                {
                                    GameController.Instance.ResetRedPuckForAllClients();
                                }
                                GameController.Instance.CallHandleMasterSwitch();
                            }
                        }
                        else
                        {
                            GameController.Instance.CallHandleMasterSwitch();
                        }
                    }

                    yield break;
                }

                // ✅ RECONNECT LOGIC - State-aware approach
                timeSinceLastAttempt += ReconnectAttemptInterval;

                if (timeSinceLastAttempt >= minTimeBetweenAttempts)
                {
                    timeSinceLastAttempt = 0f;

                    if (!PhotonNetwork.IsConnected)
                    {
                        // ❌ Not connected at all - use ReconnectAndRejoin
                        Debug.Log($"[Reconnect] Disconnected - attempting ReconnectAndRejoin (elapsed: {elapsed:F1}s)");
                        PhotonNetwork.ReconnectAndRejoin();
                    }
                    else if (PhotonNetwork.InLobby && !string.IsNullOrEmpty(savedRoomName))
                    {
                        // 🔄 In lobby - try to rejoin the saved room
                        Debug.Log($"[Reconnect] In lobby - attempting RejoinRoom({savedRoomName}) (elapsed: {elapsed:F1}s)");
                        PhotonNetwork.RejoinRoom(savedRoomName);
                    }
                    else if (PhotonNetwork.IsConnectedAndReady && !PhotonNetwork.InRoom && !PhotonNetwork.InLobby)
                    {
                        // ⚠️ Connected to master but not in lobby - join lobby first
                        Debug.Log($"[Reconnect] Connected but not in lobby - joining lobby (elapsed: {elapsed:F1}s)");
                        PhotonNetwork.JoinLobby();
                    }
                    else
                    {
                        // ℹ️ In some transition state - wait
                        Debug.Log($"[Reconnect] Waiting... IsConnected:{PhotonNetwork.IsConnected} InRoom:{PhotonNetwork.InRoom} InLobby:{PhotonNetwork.InLobby} (elapsed: {elapsed:F1}s)");
                    }
                }

                yield return new WaitForSeconds(ReconnectAttemptInterval);
                elapsed += ReconnectAttemptInterval;
            }

            Debug.LogError($"[Reconnect] Failed after {ReconnectPanelTimeoutSeconds}s");
            Debug.LogError($"[Reconnect] Final - IsConnected:{PhotonNetwork.IsConnected} InRoom:{PhotonNetwork.InRoom} InLobby:{PhotonNetwork.InLobby}");

            if (GameController.Instance != null)
            {
                GameController.Instance.HideReconnecting();
                showingReconnectUI = false;
                GameController.Instance.ShowDisconnectLose();
            }

            reconnectCoroutine = null;
            localDisconnectHandled = false;
        }
        private IEnumerator TryReconnectThenShowPanel()
        {
            reconnecting = true;
            float elapsed = 0f;

            while (elapsed < RejoinTimeoutSeconds)
            {
                if (PhotonNetwork.ReconnectAndRejoin())
                {
                    // give time for the rejoin to complete
                    yield return new WaitForSeconds(0.5f);
                    if (PhotonNetwork.InRoom)
                    {
                        reconnecting = false;
                        yield break; // rejoined successfully; no panel
                    }
                }

                yield return new WaitForSeconds(0.5f);
                elapsed += 0.5f;
            }

            reconnecting = false;

            // Only show panel if we're in Game AND not in a room
            if (SceneManager.GetActiveScene().name == "Game" &&
                GameController.Instance != null &&
                !PhotonNetwork.InRoom)
            {
                Debug.Log("[Reconnect] Failed to rejoin within grace window – showing disconnect panel");
                GameController.Instance.ShowDisconnectLose();
            }
        }
    }
}