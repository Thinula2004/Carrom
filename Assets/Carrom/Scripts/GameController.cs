using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using Unity.Properties;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BEKStudio
{
    public class GameController : MonoBehaviourPunCallbacks
    {
        public static GameController Instance;
        public enum GameState { READY, WAIT, SWITCH_MASTER, WIN, LOSE };
        public GameState gameState;
        public enum WhichPlayer { ME, OTHER };
        public WhichPlayer whichPlayer;
        public Rigidbody2D playerPuck;
        public CircleCollider2D CircleCollider2D;
        public PhotonView playerPhotonView;
        public List<GameObject> homePucksCollected;
        public List<GameObject> awayPucksCollected;
        public List<GameObject> pucksCollected;
        public Sprite[] avatars;
        [Header("Top")]
        public TextMeshProUGUI topTotalBetText;
        //public Image topLeftAvatar;
        public Image topLeftAvatarTimer;
        public TextMeshProUGUI topLeftNameText;
        public TextMeshProUGUI topLeftScoreText;
        public TextMeshProUGUI PlayerNameGameWin;
        public Image topLeftpuckIcon;
        //public Image topRightAvatar;
        public Image topRightAvatarTimer;
        public TextMeshProUGUI topRightNameText;
        public TextMeshProUGUI topRightScoreText;
        public Image topRightpuckIcon;

        public Image topWhiteAvatar;
        public Image topWhiteAvatarTimer;
        public TextMeshProUGUI topWhiteNameText;
        public TextMeshProUGUI topWhiteScoreText;
        public Image topWhitepuckIcon;
        public Image topBlackAvatar;
        public Image topBlackAvatarTimer;
        public TextMeshProUGUI topBlackNameText;
        public TextMeshProUGUI topBlackScoreText;
        public Image topBlackpuckIcon;

        [SerializeField] Sprite whitePuckSprite;
        [SerializeField] Sprite blackPuckSprite;

        public Transform topCoinParentForAnim;
        [Header("Toast Message")]
        public GameObject toastMessage;
        [Header("Settings")]
        public GameObject settingsScreen;
        public Image settingsBackground;
        public RectTransform settingsContinue;
        public RectTransform settingsMenu;
        public Slider playerSlider;
        public Slider opponentSlider;
        public GameObject playerSliderBG;
        public GameObject opponentSliderBG;
        [Header("Puck")]
        public Puck[] allPucks;
        public float playerPuckMinX;
        public float playerPuckMaxX;
        [Header("Others")]
        public bool isPucksFixed;
        public bool shootAgain;
        string targetTag;
        public string masterClientTag;
        public float timer = 5;
        public bool reduceTimer;
        public GameObject redPuck;
        public GameObject statusScreen;
        public Image statusBackground;
        public RectTransform statusPanel;
        public TextMeshProUGUI statusPanelText;

        public GameObject WinImg;
        public GameObject loseImg;
        public GameObject startImg;
        public GameObject errorPanel;

        public bool redPuckCollected;
        public bool practiceMode;
        public bool redPuckWaiting;

        private string roomName;
        public Sprite[] bg_sprites;
        public Image bgImage;
        public string PlayerimageUrl;
        public string OpponentimageUrl;

        [SerializeField] private AudioSource audioSource;

        private float lastValueChangeTime;
        private bool isSliding = false;

        private string RoomName;
        private int roomEntryPice;
        private int roomWinPrice;

        public Image ProfiletargetImage;
        public Image OpponenttargetImage;
        public Image PlayerImgGameWin;
        public GameObject NamePanelGameWin;
        public Sprite defaultSprite;

        // PhotonView reference for RPC calls
        private PhotonView photonView;

        // NEW: track the color of the last pocketed coin in this turn ("White", "Black", or "")
        private string lastPocketedColor = "";

        public GameObject[] gameOverBtns;
        private bool isAllFinished = false;
        public GameObject WinBetAnim;

        public GameObject HomeBtn;

        // NEW: Allow/disallow pocketing. We'll disable pocketing during turn changes to prevent coins
        // being counted while master/client switching is happening.
        [HideInInspector]
        public bool allowPocketing = true;

        public GameObject DiscBlinker;

        private bool winnerAnnounced = false;

        // NEW: keep who started the turn (for 9th coin rule stability)
        private string shooterTagAtTurnStart = "";

        // NEW: UI indicator for red collected
        public GameObject redCoinIcon;
        [Header("Disconnect Panel")]
        public GameObject disconnectLosePanel;
        public GameObject reconnectingPanel;
        private bool reconnectingVisible = false;
        public GameObject waitingForOpponentText;
        public GameObject LoseAPIErrorPanel, LoadingPanel;

        // Round id: single source of truth from room property
        private const string ROOMPROP_ROUND_ID = "round_id";
        private string cachedRoundId = null;
        // Add near other fields
        private bool matchStarted = false;

        private Dictionary<int, float> lastPings = new Dictionary<int, float>();

        bool isFirstSwitch = false;

        bool isFirstTurnStarted = false;

        private bool isWaitingForTurnStart = false;
        private float turnStartTimeout = 0f;
        private const float TURN_START_MAX_WAIT = 3f;
        //public const string ROOMPROP_BET_COUNT = "BetCount";

        public GameObject disk;

        public bool isPlayerOnTheTab = true;

        public bool isTurnHardRestarting;

        public Transform mainCam;
        public float focusTime;
        [HideInInspector]public bool redPuckCollectedLocal = false;
        public const string BOTTOM_RIGHT_SECTION = "BottomRightSection", BOTTOM_LEFT_SECTION = "BottomLeftSection", TOP_RIGHT_SECTION = "TopRightSection", TOP_LEFT_SECTION = "TopLeftSection";
        public const string BOTTOM_LEFT_HOLE = "Left Down", BOTTOM_RIGHT_HOLE = "Right Down", TOP_LEFT_HOLE = "Left Up", TOP_RIGHT_HOLE = "Right Up";
        public const string LEFT_WALL = "LeftWall", RIGHT_WALL = "RightWall", TOP_WALL = "TopWall", BOTTOM_WALL = "BottomWall", NO_WALL = "NoWall";
        float lastOnlineTime;
        Coroutine offlineReconnectFlow;

        public bool hasBroken;
        public int turnCount = 0;
        public string botRoundId;

        public Sprite tutorialSprite, tutorialSpriteReverse;

        public Image redPuckConfirmationImg;
        public TMP_Text versionTxt;

        void Awake()
        {
            //PhotonNetwork.SendRate = 30;
            PhotonNetwork.SerializationRate = 25;
    
            NamePanelGameWin.SetActive(false);
            WinBetAnim.SetActive(false);
            HomeBtn.SetActive(false);
            if (Instance == null)
            {
                Instance = this;
            }
            photonView = GetComponent<PhotonView>();
            // Get PhotonView component
            //photonView = GetComponent<PhotonView>();
            //if (photonView == null)
            //{
            //    photonView = gameObject.AddComponent<PhotonView>();
            //}
            // ✅ Add error check instead:
            if (photonView == null)
            {
                Debug.LogError("GameController MUST have a PhotonView component added in the Unity Editor!");
            }
        }

        void Start()
        {
            versionTxt.text = VersionChecker.GetVersion();

            lastOnlineTime = Time.realtimeSinceStartup;
            hasBroken = false;
            offlineReconnectFlow = null;
            if (!PhotonController.Instance.offlineMatch)
            {    
                ExitGames.Client.Photon.Hashtable rp = new ExitGames.Client.Photon.Hashtable();
                rp["currentTurnPlayer"] = PhotonNetwork.LocalPlayer.ActorNumber;
                PhotonNetwork.CurrentRoom.SetCustomProperties(rp);
            }
            redPuckCollectedLocal = false;
            isTurnHardRestarting = false;
            isWaitingForTurnStart = false;
            turnStartTimeout = 0f;
            PlayerPuck.Instance.EnableTouchControls();
            DiscBlinker.SetActive(false);
            AudioManager.Instance.StopTimeoutSound();
            Application.targetFrameRate = 40;
            PlayerimageUrl = PlayerPrefs.GetString("profileimage");
            OpponentimageUrl = PlayerPrefs.GetString("opponentimage");
            string PlayerNameTemp = PhotonNetwork.LocalPlayer.NickName;
            PlayerNameGameWin.text = LimitNameText(PlayerNameTemp);
            
            LoadingPanel.SetActive(true);
            StartCoroutine(LoadingCoroutine());

            if (!string.IsNullOrEmpty(PlayerimageUrl))
                StartCoroutine(LoadImageFromUrl(PlayerimageUrl, ProfiletargetImage, defaultSprite));

            if (!PhotonController.Instance.offlineMatch)
            {
                if (!string.IsNullOrEmpty(OpponentimageUrl))
                StartCoroutine(LoadImageFromUrl(OpponentimageUrl, OpponenttargetImage, defaultSprite));
            }
            else
            {
                OpponenttargetImage.sprite = PhotonController.Instance.demoPlayer != null ? PhotonController.Instance.demoPlayer.profilePic : defaultSprite;
            }
            
            RoomName = PlayerPrefs.GetString("roomName");
            AudioManager.Instance.StopRollingSound();

            RoomNamePrice();
            Debug.Log("roomentry price2: " + roomEntryPice);
            AudioManager.Instance.StopRollingSound();
            roomName = PlayerPrefs.GetString("roomName");

            if (roomName == "colombo")
            {
                bgImage.sprite = bg_sprites[0];
            }
            else if (roomName == "galle")
            {
                bgImage.sprite = bg_sprites[1];
            }
            else if (roomName == "kandy")
            {
                bgImage.sprite = bg_sprites[2];
            }
            else if (roomName == "sigiri")
            {
                bgImage.sprite = bg_sprites[3];
            }
            else if (roomName == "jaffna")
            {
                bgImage.sprite = bg_sprites[4];
            }

            if (!PhotonController.Instance.offlineMatch)
            {
                if (!PhotonNetwork.IsMasterClient)
                {
                    whichPlayer = WhichPlayer.OTHER;

                    //topWhiteAvatar = topRightAvatar;
                    topWhiteAvatarTimer = topRightAvatarTimer;
                    topWhiteNameText = topRightNameText;
                    topWhiteScoreText = topRightScoreText;

                    //topBlackAvatar = topLeftAvatar;
                    topBlackAvatarTimer = topLeftAvatarTimer;
                    topBlackNameText = topLeftNameText;
                    topBlackScoreText = topLeftScoreText;

                    topRightpuckIcon.sprite = whitePuckSprite;
                    topLeftpuckIcon.sprite = blackPuckSprite;

                }
                else
                {
                    //topWhiteAvatar = topLeftAvatar;
                    topWhiteAvatarTimer = topLeftAvatarTimer;
                    topWhiteNameText = topLeftNameText;
                    topWhiteScoreText = topLeftScoreText;

                    //topBlackAvatar = topRightAvatar;
                    topBlackAvatarTimer = topRightAvatarTimer;
                    topBlackNameText = topRightNameText;
                    topBlackScoreText = topRightScoreText;

                    topRightpuckIcon.sprite = blackPuckSprite;
                    topLeftpuckIcon.sprite = whitePuckSprite;
                }

                if(PhotonNetwork.LocalPlayer.ActorNumber != 1)
                {
                    Quaternion camRot = mainCam.rotation;
                    camRot.z = -180;
                    mainCam.rotation = camRot;

                    PlayerPuck.Instance.tutorial.GetComponent<SpriteRenderer>().sprite = tutorialSpriteReverse;
                }
                else
                {
                    PlayerPuck.Instance.tutorial.GetComponent<SpriteRenderer>().sprite = tutorialSprite;
                }

                    // Initialize masterClientTag from the actual current master client
                if (PhotonNetwork.InRoom && PhotonNetwork.MasterClient != null && PhotonNetwork.MasterClient.CustomProperties.ContainsKey("tag"))
                {
                    masterClientTag = (string)PhotonNetwork.MasterClient.CustomProperties["tag"];
                }
                else
                {
                    masterClientTag = "White"; // fallback
                }

                var p = new ExitGames.Client.Photon.Hashtable { { "loadedGame", true } };
                PhotonNetwork.LocalPlayer.SetCustomProperties(p);

                targetTag = (string)PhotonNetwork.LocalPlayer.CustomProperties["tag"];

                // NEW: Initialize red state from room property (handles late join/reconnect)
                ApplyRedCollectedFromSharedState(GetRedCollectedFromRoom());

                SetTopUsersForGame();

                UpdateRedIcon();

                ExitGames.Client.Photon.Hashtable roomProperties = new ExitGames.Client.Photon.Hashtable();
                roomProperties["intentionalSwitch"] = false;
                PhotonNetwork.CurrentRoom.SetCustomProperties(roomProperties);

                InvokeRepeating("PingSet", 2f, 0.5f);
                InvokeRepeating("PingCheck", 2f, 0.5f);
            }
            else
            {
                topWhiteAvatarTimer = topLeftAvatarTimer;
                topWhiteNameText = topLeftNameText;
                topWhiteScoreText = topLeftScoreText;

                topBlackAvatarTimer = topRightAvatarTimer;
                topBlackNameText = topRightNameText;
                topBlackScoreText = topRightScoreText;

                topRightpuckIcon.sprite = blackPuckSprite;
                topLeftpuckIcon.sprite = whitePuckSprite;

                masterClientTag = "White";

                targetTag = "White";

                StartCoroutine(InvokeRepeatingUnscaled(CheckInternet, 2f, 2f));
                SetTopUsersForBot();
                SetupPuckDampings();

                BR_APIManager.Instance.UpdateBotCount();
            }

            shooterTagAtTurnStart = masterClientTag;

            // CheckTurn();

            homePucksCollected = new List<GameObject>();
            awayPucksCollected = new List<GameObject>();
            pucksCollected = new List<GameObject>();

            // redPuck.SetActive(PhotonController.Instance.whichMode == "carrom");

            topTotalBetText.text = (PhotonController.Instance.roomEntryPice * 2).ToString("###,###,###") + "LKR";

            isPucksFixed = true;

            // GameController.Start()
            if (disconnectLosePanel != null) disconnectLosePanel.SetActive(false);
            if (reconnectingPanel != null) reconnectingPanel.SetActive(false);
            if (waitingForOpponentText != null) waitingForOpponentText.SetActive(false);
            if (LoseAPIErrorPanel != null) LoseAPIErrorPanel.SetActive(false);

            if(PhotonController.Instance.offlineMatch)
            {
                RPC_CheckTurn();
            }
        }

        void SetupPuckDampings()
        {
            foreach(Puck puck in allPucks)
            {
                if (puck.CompareTag("White"))
                {
                    puck.GetComponent<Rigidbody2D>().linearDamping = 4;
                }
            }
        }

        IEnumerator LoadingCoroutine()
        {
            yield return new WaitForSeconds(20f);

            if (LoadingPanel.activeInHierarchy && !PhotonController.Instance.offlineMatch)
            {
                PhotonController.Instance.MarkIntentionalLeave();
                PhotonNetwork.LeaveRoom();
                LoseAPIErrorPanel.SetActive(true);
            }
        }

        public bool AllLoaded()
        {
            foreach (var p in PhotonNetwork.PlayerList)
                if (!p.CustomProperties.TryGetValue("loadedGame", out var v) || !(v is bool b) || !b)
                    return false;
            return true;
        }

        [PunRPC]
        void RPC_CheckTurn()
        {
            LoadingPanel.SetActive(false);
            StartCoroutine(WaitForPlayersBeforeBetting());
            StartMessage();
            CheckTurn();
        }

        private void PingSet()
        {

            if (!PhotonNetwork.IsConnectedAndReady)
            {
                // Check how long we've been unable to ping
                float timeSinceLastPing = Time.time - PhotonController.Instance.lastSuccessfulPingTime;

                // ✅ Use PhotonController.LocalDisconnectThreshold (it's a const, access via class name)
                if (timeSinceLastPing > PhotonController.LocalDisconnectThreshold)
                {
                    if (!PhotonController.Instance.localDisconnectHandled)
                    {
                        Debug.LogWarning($"⚠️ Local disconnect detected! No successful ping for {timeSinceLastPing:F1}s");
                        PhotonController.Instance.HandleLocalDisconnect();
                    }
                }
                return;
            }


            if (isPlayerOnTheTab)
            {
                ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
                props["lastPing"] = PhotonNetwork.ServerTimestamp;
                PhotonNetwork.LocalPlayer.SetCustomProperties(props);
                PhotonController.Instance.lastSuccessfulPingTime = Time.time;
                if (PhotonController.Instance.localDisconnectHandled)
                {
                    PhotonController.Instance.localDisconnectHandled = false;
                }
                // Debug.Log("D: Ping Set to "+props["lastPing"]);
            }
        }

        void OnApplicationFocus(bool hasFocus)
        {
            // Debug.Log("Focus: " + hasFocus);
            if (!PhotonController.Instance.offlineMatch)
            {
                if (!hasFocus)
                {
                    Debug.Log("Player switched tab.");
                    isPlayerOnTheTab = false;
                }
                else
                {
                    Debug.Log("Player returned.");
                    if(isPlayerOnTheTab == false)
                    {
                        isTurnHardRestarting = true;
                        isPlayerOnTheTab = true;
                        if (!PhotonNetwork.IsMasterClient)
                        {
                            CallHandleMasterSwitch();
                        }
                    }
                }
            }
            else
            {
                if (!hasFocus)
                {
                    focusTime = Time.realtimeSinceStartup;
                }
                else
                {
                    Debug.Log($"Out Time {focusTime}\nIn Time {Time.realtimeSinceStartup}\n Diff {Time.realtimeSinceStartup - focusTime}");
                    float diff = Time.realtimeSinceStartup - focusTime;
                    if(diff >= 15)
                    {
                        HideDiskAndPucks();
                        ShowDisconnectLose();
                    }
                }
            }
        }

        private void PingCheck()
        {
            PhotonController.Instance.CheckPing();
        }

        public void ShowReconnecting()
        {
            if (winnerAnnounced || gameState == GameState.WIN || gameState == GameState.LOSE) return;
            if (reconnectingPanel == null) return;

            if (!reconnectingVisible)
            {
                reconnectingVisible = true;
                reconnectingPanel.SetActive(true);
            }
        }

        public void HideReconnecting()
        {
            if (reconnectingPanel == null) return;

            if (reconnectingVisible)
            {
                reconnectingVisible = false;
                reconnectingPanel.SetActive(false);
            }
        }

        public void ShowWaitingForOpponent()
        {
            
            if (winnerAnnounced || gameState == GameState.WIN || gameState == GameState.LOSE) return;

            if (waitingForOpponentText != null) 
            { 
                waitingForOpponentText.SetActive(true); 
                PlayerPuck.Instance.DisableTouchControls(); 
                isTurnHardRestarting = true;
            }
        }

        public void HideWaitingForOpponent()
        {
            if (waitingForOpponentText != null)
            {
                waitingForOpponentText.SetActive(false);
                Invoke("EnableTouchDelay", .5f);
            }
        }

        public void EnableTouchDelay()
        {
            PlayerPuck.Instance.EnableTouchControls();
        }

        public void ShowDisconnectLose()
        {
            // ✅ Extra safety: Don't show if already showing
            if (disconnectLosePanel != null && disconnectLosePanel.activeSelf)
            {
                Debug.Log("[Disconnect] Panel already showing - ignoring");
                return;
            }

            Debug.Log("[Disconnect] ✅ Showing disconnect lose panel");

            // Stop the game
            gameState = GameState.LOSE;
            reduceTimer = false;
            allowPocketing = false;
            AudioManager.Instance.StopTimeoutSound();

            // Show disconnect panel
            if (disconnectLosePanel != null)
            {
                disconnectLosePanel.SetActive(true);
                PhotonController.Instance.MarkIntentionalLeave();
                if (PhotonNetwork.InRoom)
                {
                    PhotonNetwork.LeaveRoom();
                }
            }
        }
        private IEnumerator WaitForPlayersBeforeBetting()
        {
            // ✅ CRITICAL: Wait for Photon to fully sync after scene load
            Debug.Log("⏳ Waiting for Photon to sync player states...");
            yield return new WaitForSeconds(2f); // Give Photon time to sync

            if (PhotonController.Instance.offlineMatch)
            {
                StartCoroutine(WaitAndPostBet());
                yield break;
            }

            float waitTime = 0f;
            float maxWaitTime = 8f;
            int stableCount = 0;
            int lastPlayerCount = PhotonNetwork.PlayerList.Length;

            Debug.Log($"Initial player count: {lastPlayerCount}");

            // Wait until we have STABLE 2 players OR timeout
            while (waitTime < maxWaitTime)
            {
                int currentCount = PhotonNetwork.PlayerList.Length;

                // Check if count is stable (same for 3 consecutive checks)
                if (currentCount == lastPlayerCount && currentCount == 2)
                {
                    stableCount++;
                    if (stableCount >= 3) // Stable for 0.3 seconds
                    {
                        Debug.Log("✅ 2 players STABLE. Proceeding with bet...");
                        break;
                    }
                }
                else
                {
                    stableCount = 0; // Reset if count changed
                    lastPlayerCount = currentCount;

                    if (currentCount < 2)
                    {
                        Debug.Log($"⚠️ Player count dropped to {currentCount}. Waiting...");
                    }
                }

                yield return new WaitForSeconds(0.1f);
                waitTime += 0.1f;
            }
            yield return new WaitForSeconds(1f);
            // Final check
            if (PhotonNetwork.PlayerList.Length != 2)
            {
                Debug.Log($"❌ Final check: Only {PhotonNetwork.PlayerList.Length} player(s). Leaving...");

                if (PhotonNetwork.InRoom)
                {
                    PhotonController.Instance.MarkIntentionalLeave();
                    PhotonNetwork.LeaveRoom();

                }
                LeaveFromGame();
                yield break;
            }

            Debug.Log("✅ 2 players confirmed! Posting bets...");

            if (PhotonNetwork.IsMasterClient)
            {
                StartCoroutine(WaitAndPostBet());
            }
            else
            {
                StartCoroutine(DelayedWaitAndPostBet());
            }
        }

        private IEnumerator DelayedWaitAndPostBet()
        {
            yield return new WaitForSeconds(1f); // Wait for master to set properties
            yield return StartCoroutine(WaitAndPostBet());
        }

        // ✅ NEW: Override Photon callback to listen for room property changes
        public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
        {
            // Winner sync
            if (propertiesThatChanged.ContainsKey("GameWinner"))
            {
                if (winnerAnnounced) return; // already handled
                winnerAnnounced = true;

                string winnerTag = (string)propertiesThatChanged["GameWinner"];
                gameState = (targetTag == winnerTag) ? GameState.WIN : GameState.LOSE;
                reduceTimer = false; // stop timer
                GameOver();
                return;
            }

            // NEW: Red collected sync via Room Property
            if (propertiesThatChanged.ContainsKey(ROOMPROP_RED_COLLECTED))
            {
                bool val = false;
                object v = propertiesThatChanged[ROOMPROP_RED_COLLECTED];
                if (v is bool b) val = b;

                ApplyRedCollectedFromSharedState(val);
            }
        }

        public override void OnPlayerPropertiesUpdate(Player target, ExitGames.Client.Photon.Hashtable changedProps)
        {
            if(PhotonNetwork.IsMasterClient && AllLoaded() && !isFirstTurnStarted)
            {
                isFirstTurnStarted = true;
                if(PhotonNetwork.MasterClient.ActorNumber != 1)
                {
                    // isFirstSwitch = true;
                    // PhotonNetwork.SetMasterClient(PhotonNetwork.MasterClient.GetNext());
                }
                else
                {
                    photonView.RPC("RPC_CheckTurn", RpcTarget.All);        
                }
            }
        }

        public void CallHandleMasterSwitch()
        {
            photonView.RPC("RPC_HandleMasterSwitch", RpcTarget.All);
        }

        [PunRPC]
        void RPC_HandleMasterSwitch()
        {
            HandleMasterSwitched(PhotonNetwork.MasterClient, true);
        }

        // ✅ NEW: Keep masterClientTag in sync when master switches (networked turn handoff)
        public override void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
        {
            if (isFirstSwitch)
            {
                HandleMasterSwitched(newMasterClient, true);
                isFirstSwitch = false;
                return;
            }

            isWaitingForTurnStart = false;
            bool isIntentional = false;
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("intentionalSwitch", out object val))
            {
                isIntentional = val is bool b && b;
            }

            HandleMasterSwitched(newMasterClient, isIntentional);
        }

        public void HandleMasterSwitched(Photon.Realtime.Player newMasterClient, bool isIntentional)
        {
            if (winnerAnnounced || gameState == GameState.WIN || gameState == GameState.LOSE || !isIntentional)
            {
                Debug.Log("Skipping turn logic due to unintentional switch.");
                return;
            }

            Debug.Log("D: 3 — Proceeding with turn change");

            if (PhotonNetwork.IsMasterClient)
            {
                ExitGames.Client.Photon.Hashtable resetProps = new ExitGames.Client.Photon.Hashtable();
                resetProps["intentionalSwitch"] = false;
                PhotonNetwork.CurrentRoom.SetCustomProperties(resetProps);

                ExitGames.Client.Photon.Hashtable roomProps = new ExitGames.Client.Photon.Hashtable();
                roomProps["currentTurnPlayer"] = PhotonNetwork.LocalPlayer.ActorNumber;
                Debug.Log("D: Current Player: " + roomProps["currentTurnPlayer"]);
                PhotonNetwork.CurrentRoom.SetCustomProperties(roomProps);
            }

            Debug.Log($"🔀🔀🔀🔀🔀🔀🔀🔀🔀🔀🔀🔀🔀🔀🔀🔀🔀🔀🔀🔀");
            Debug.Log($"[{targetTag}] OnMasterClientSwitched called");
            Debug.Log($"[{targetTag}] OLD masterClientTag: {masterClientTag}");
            Debug.Log($"[{targetTag}] OLD IsMasterClient: {PhotonNetwork.IsMasterClient}");

            if (newMasterClient != null && newMasterClient.CustomProperties.ContainsKey("tag"))
            {
                masterClientTag = (string)newMasterClient.CustomProperties["tag"];
            }
            whichPlayer = (newMasterClient == PhotonNetwork.LocalPlayer) ? WhichPlayer.ME : WhichPlayer.OTHER;

            shooterTagAtTurnStart = masterClientTag;

            Debug.Log($"[{targetTag}] NEW masterClientTag: {masterClientTag}");
            Debug.Log($"[{targetTag}] NEW IsMasterClient: {PhotonNetwork.IsMasterClient}");
            Debug.Log($"[{targetTag}] NEW whichPlayer: {whichPlayer}");
            Debug.Log($"[{targetTag}] redPuckCollected: {redPuckCollected}");
            Debug.Log($"🔀🔀🔀🔀🔀🔀🔀🔀🔀🔀🔀🔀🔀🔀🔀🔀🔀🔀🔀🔀");

            // Re-enable pocketing and prepare the next turn
            //allowPocketing = true;
            CheckTurn();
        }

        /// <summary>
        /// Gets or creates a shared round ID for the current race.
        /// Master client creates it, other clients wait for it.
        /// </summary>
        private IEnumerator EnsureRoundIdCoroutine(Action<string> callback)
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            {
                Debug.LogError("[RoundId] Not in a room. Cannot ensure round_id.");
                callback?.Invoke(null);
                yield break;
            }

            // 1) If already cached locally, use it
            if (!string.IsNullOrEmpty(cachedRoundId))
            {
                callback?.Invoke(cachedRoundId);
                yield break;
            }

            // 2) If room property already exists, cache + return
            if (PhotonNetwork.CurrentRoom.CustomProperties != null &&
                PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(ROOMPROP_ROUND_ID))
            {
                cachedRoundId = PhotonNetwork.CurrentRoom.CustomProperties[ROOMPROP_ROUND_ID]?.ToString();
                callback?.Invoke(cachedRoundId);
                yield break;
            }

            // 3) Master: create exactly once (race-safe using "expected")
            if (PhotonNetwork.IsMasterClient)
            {
                // Optional hardening: wait briefly until 2 players are present (if your game requires 2)
                float playersWait = 0f;
                while (playersWait < 5f && (PhotonNetwork.PlayerList == null || PhotonNetwork.PlayerList.Length < 2))
                {
                    yield return new WaitForSeconds(0.1f);
                    playersWait += 0.1f;
                }

                // Double-check again right before creating (race safety)
                if (PhotonNetwork.CurrentRoom.CustomProperties != null &&
                    PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(ROOMPROP_ROUND_ID))
                {
                    cachedRoundId = PhotonNetwork.CurrentRoom.CustomProperties[ROOMPROP_ROUND_ID]?.ToString();
                    callback?.Invoke(cachedRoundId);
                    yield break;
                }

                string newId = BuildRoundId();

                var props = new ExitGames.Client.Photon.Hashtable
        {
            { ROOMPROP_ROUND_ID, newId }
        };

                // ✅ Race-safe: only set if it doesn't exist yet
                var expected = new ExitGames.Client.Photon.Hashtable
        {
            { ROOMPROP_ROUND_ID, null }
        };

                bool success = PhotonNetwork.CurrentRoom.SetCustomProperties(props, expected);

                if (success)
                {
                    cachedRoundId = newId;
                    Debug.Log("[RoundId] Master successfully set round_id: " + newId);
                    callback?.Invoke(newId);
                    yield break;
                }

                // If we failed, someone else set it first. Wait until it shows up and use it.
                Debug.LogWarning("[RoundId] round_id already set by another client. Waiting to read it...");

                float readTimeout = 5f;
                float readElapsed = 0f;

                while (readElapsed < readTimeout)
                {
                    if (PhotonNetwork.CurrentRoom.CustomProperties != null &&
                        PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(ROOMPROP_ROUND_ID))
                    {
                        cachedRoundId = PhotonNetwork.CurrentRoom.CustomProperties[ROOMPROP_ROUND_ID]?.ToString();
                        Debug.Log("[RoundId] Master read existing round_id after failed set: " + cachedRoundId);
                        callback?.Invoke(cachedRoundId);
                        yield break;
                    }

                    yield return new WaitForSeconds(0.1f);
                    readElapsed += 0.1f;
                }

                Debug.LogError("[RoundId] Failed to read round_id after SetCustomProperties failed.");
                callback?.Invoke(null);
                yield break;
            }

            // 4) Non-master: wait until master sets it (NO fallback generation)
            float timeout = 10f;
            float elapsed = 0f;

            while (elapsed < timeout)
            {
                if (PhotonNetwork.CurrentRoom.CustomProperties != null &&
                    PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(ROOMPROP_ROUND_ID))
                {
                    cachedRoundId = PhotonNetwork.CurrentRoom.CustomProperties[ROOMPROP_ROUND_ID]?.ToString();
                    Debug.Log("[RoundId] Non-master received round_id: " + cachedRoundId);
                    callback?.Invoke(cachedRoundId);
                    yield break;
                }

                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            Debug.LogError("[RoundId] Timeout waiting for master to set round_id. NOT generating fallback (to avoid mismatch).");
            callback?.Invoke(null);
        }

        private string BuildRoundId()
        {
            string rn = string.IsNullOrEmpty(roomName) ? "room" : roomName;

            string p1 = PhotonNetwork.PlayerList.Length > 0 ? PhotonNetwork.PlayerList[0].NickName : "p1";
            string p2 = PhotonNetwork.PlayerList.Length > 1 ? PhotonNetwork.PlayerList[1].NickName : "p2";

            p1 = NormalizeIdPart(p1);
            p2 = NormalizeIdPart(p2);
            rn = NormalizeIdPart(rn);

            // Sort names so order is stable
            string a = p1, b = p2;
            if (string.CompareOrdinal(a, b) > 0)
            {
                var tmp = a; a = b; b = tmp;
            }

            // Server-synced-ish time based on PhotonNetwork.Time
            int secondsOfDay = (int)(PhotonNetwork.Time % 86400);
            int hh = secondsOfDay / 3600;
            int mm = (secondsOfDay % 3600) / 60;
            int ss = secondsOfDay % 60;
            string time = $"{hh:D2}{mm:D2}{ss:D2}";

            // ✅ Add 3 random digits (Master only should call BuildRoundId)
            int rand3 = UnityEngine.Random.Range(0, 1000); // 0..999
            string r = rand3.ToString("D3");

            return $"{a}_{b}_{rn}_{time}_{r}";
        }

        private string BuildBotRoundID()
        {
            string rn = string.IsNullOrEmpty(roomName) ? "room" : roomName;

            string p1 = NormalizeIdPart(PhotonNetwork.LocalPlayer.NickName);
            string p2 = NormalizeIdPart(PhotonController.Instance.demoPlayer.name);
            rn = NormalizeIdPart(rn);
            
            int secondsOfDay = (int)(PhotonNetwork.Time % 86400);
            int hh = secondsOfDay / 3600;
            int mm = secondsOfDay % 3600 / 60;
            int ss = secondsOfDay % 60;
            string time = $"{hh:D2}{mm:D2}{ss:D2}";

            int rand3 = UnityEngine.Random.Range(0, 1000); // 0..999
            string r = rand3.ToString("D3");

            return $"{p1}_[DEMO]{p2}_{rn}_{time}_{r}";
        }

        private string NormalizeIdPart(string s)
        {
            if (string.IsNullOrEmpty(s)) return "x";
            s = s.Trim().ToLowerInvariant();

            // keep only [a-z0-9], replace others with nothing
            System.Text.StringBuilder sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s)
            {
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                    sb.Append(c);
            }

            // avoid empty parts
            return sb.Length > 0 ? sb.ToString() : "x";
        }

        private IEnumerator WaitAndPostBet()
        {
            yield return null;

            string roundId = null;

            if (!PhotonController.Instance.offlineMatch)
            {
                yield return StartCoroutine(EnsureRoundIdCoroutine((id) => roundId = id));
            }
            else
            {
                botRoundId = BuildBotRoundID();
                roundId = botRoundId;
            }

            // Wait for round ID to be available

            if (string.IsNullOrEmpty(roundId))
            {
                Debug.LogError("[WaitAndPostBet] round_id is null/empty. Aborting debit.");
                yield break;
            }

            Debug.Log("[WaitAndPostBet] Using round_id: " + roundId);

            const int maxAttempts = 2;
            //const float requestTimeout = 15f; // safety timeout per attempt
            const float retryDelay = 1f;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                bool apiCompleted = false;
                bool apiSuccess = false;
                string apiMessage = "";

                Debug.Log($"[CanvasManager] Bet Debit attempt {attempt}/{maxAttempts}...");

                BR_APIManager.Instance.PostBetTransaction(
                    roomEntryPice,
                    "debit",
                    roundId,
                    (success, message) =>
                    {
                        apiSuccess = success;
                        apiMessage = message;
                        apiCompleted = true;
                    }
                );

                // Wait until callback response OR timeout safety
                yield return new WaitUntil(() => apiCompleted);

                if (apiSuccess)
                {
                    Debug.Log("[CanvasManager] Bet Debit SUCCESS: " + apiMessage);
                    if (!PhotonController.Instance.offlineMatch)
                    {
                        IncrementBetCount();
                    }
                    matchStarted = true;
                    yield break; // ✅ success, don't call again
                }


                // failed (or timeout) -> retry if attempts remain
                if (attempt < maxAttempts)
                {
                    yield return new WaitForSeconds(retryDelay);
                }
            }

            LeaveFromGame();
            Debug.LogError("[CanvasManager] Bet Debit FAILED after 3 attempts");
            // If you want: show error UI / leave game / block gameplay, do it here.
            // errorPanel.SetActive(true);
        }

        public void IncrementBetCount()
        {
            

            int lossCount = 0;
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("BetCount", out object val))
            {
                lossCount = (int)val;
            }
            Debug.Log("before increment: "+ lossCount);
            ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
            props["BetCount"] = lossCount + 1;
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
            

        }

        private IEnumerator WaitAndWinCredit()
        {
            yield return null;

            string roundId = null;
            if (!PhotonController.Instance.offlineMatch)
            {
                yield return StartCoroutine(EnsureRoundIdCoroutine((id) => roundId = id));
            }
            else
            {
                roundId = botRoundId;
            }

            Debug.Log("[WaitAndWinCredit] Using round_id: " + roundId);

            const int maxAttempts = 2;
            //const float requestTimeout = 20f;   // safety: don't wait forever if callback never comes
            const float retryDelay = 1f;        // delay between failed attempts (optional)

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                bool apiCompleted = false;
                bool apiSuccess = false;
                string apiMessage = "";

                Debug.Log($"[CanvasManager] Bet Credit attempt {attempt}/{maxAttempts}...");

                // Call API ONCE for this attempt
                BR_APIManager.Instance.PostBetTransaction(
                    roomWinPrice,
                    "credit",
                    roundId,
                    (success, message) =>
                    {
                        apiSuccess = success;
                        apiMessage = message;
                        apiCompleted = true;
                    }
                    //GetGameStatString()
                );
                yield return new WaitUntil(() => apiCompleted);


                if (apiSuccess)
                {
                    // ✅ Success: do NOT call API again
                    Debug.Log("[CanvasManager] Bet Credit SUCCESS: " + apiMessage);

                    AudioManager.Instance.PlayWinSound();
                    WinImg.SetActive(true);
                    loseImg.SetActive(false);
                    errorPanel.SetActive(false);

                    StartCoroutine(DelayShareBtn());
                    yield break;
                }

                // If we reached here, this attempt failed (or timed out).
                if (attempt < maxAttempts)
                {
                    yield return new WaitForSeconds(retryDelay);
                }
            }

            // ❌ Failed all 3 attempts
            Debug.LogError("[CanvasManager] Bet Credit FAILED after 3 attempts");
            errorPanel.SetActive(true);
        }

        public string GetGameStatString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Pucks:");
            sb.AppendLine("---------------HOME------------------");
            foreach (GameObject puck in homePucksCollected)
            {
                sb.AppendLine($"Pocketed: {puck.name}");
            }

            sb.AppendLine("---------------AWAY------------------");
            foreach (GameObject puck in awayPucksCollected)
            {
                sb.AppendLine($"Pocketed: {puck.name}");
            }

            string redString = GetRedCollectedFromRoom() ? "Pocketted" : "Not Pocketted";
            sb.AppendLine($"Red Puck : {redString}");

            return sb.ToString();
        }


        public IEnumerator LoadImageFromUrl(string url, Image targetImage, Sprite fallback)
        {
            if (string.IsNullOrEmpty(url))
            {
                Debug.LogWarning($"[{targetImage.name}] Image URL is empty!");
                if (fallback) targetImage.sprite = fallback;
                yield break;
            }

            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.ConnectionError ||
                    request.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError($"[{targetImage.name}] Error loading image: {request.error}");
                    if (fallback) targetImage.sprite = fallback;
                }
                else
                {
                    Texture2D texture = DownloadHandlerTexture.GetContent(request);
                    Sprite newSprite = Sprite.Create(
                        texture,
                        new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f)
                    );

                    targetImage.sprite = newSprite;
                    Debug.Log($"✅ [{targetImage.name}] Image loaded successfully from URL: {url}");
                }
            }
        }

        public void RoomNamePrice()
        {
            if (RoomName == "colombo")
            {
                roomEntryPice = Constants.ROOM_COLOMBO_ENTRY_PRICE;
                roomWinPrice = Constants.ROOM_COLOMBO_WIN_PRICE;
            }
            else if (RoomName == "galle")
            {
                roomEntryPice = Constants.ROOM_GALLE_ENTRY_PRICE;
                roomWinPrice = Constants.ROOM_GALLE_WIN_PRICE;
            }
            else if (RoomName == "kandy")
            {
                roomEntryPice = Constants.ROOM_KANDY_ENTRY_PRICE;
                roomWinPrice = Constants.ROOM_KANDY_WIN_PRICE;
            }
            else if (RoomName == "sigiri")
            {
                roomEntryPice = Constants.ROOM_SIGIRI_ENTRY_PRICE;
                roomWinPrice = Constants.ROOM_SIGIRI_WIN_PRICE;
            }
            else if (RoomName == "jaffna")
            {
                roomEntryPice = Constants.ROOM_JAFFNA_ENTRY_PRICE;
                roomWinPrice = Constants.ROOM_JAFFNA_WIN_PRICE;
            }
        }

        void SetTopUsersForBot()
        {
            topWhiteNameText.text = PhotonNetwork.LocalPlayer.NickName;
            topBlackNameText.text = PhotonController.Instance.demoPlayer.name;
        }

        private string LimitNameText(string fullName, int maxLength = 8)
        {
            if (string.IsNullOrEmpty(fullName)) return "";
            return fullName.Length > maxLength ? fullName.Substring(0, maxLength) + ".." : fullName;
        }

        void SetTopUsersForGame()
        {
            if (PhotonNetwork.PlayerList.Length == 2)
            {
                topWhiteNameText.text = LimitNameText(PhotonNetwork.PlayerList[0].NickName);
                //PlayerNameGameWin.text = LimitNameText(PhotonNetwork.PlayerList[0].NickName);
                //topWhiteAvatar.sprite = avatars[(int)PhotonNetwork.PlayerList[0].CustomProperties["avatar"]]);
                Debug.Log("PlayerList[0].NickName :" + PhotonNetwork.PlayerList[0].NickName);
                //PlayerPrefs.SetString(homeName, "PlayerName");
                topBlackNameText.text = LimitNameText(PhotonNetwork.PlayerList[1].NickName);

                Debug.Log("PlayerList[1].NickName :" + PhotonNetwork.PlayerList[1].NickName);
                //topBlackAvatar.sprite = avatars[(int)PhotonNetwork.PlayerList[1].CustomProperties["avatar"]]);

            }
            else
            {
                topWhiteNameText.text = LimitNameText(PhotonNetwork.PlayerList[0].NickName);
                Debug.Log("PlayerList[0].NickName :" + PhotonNetwork.PlayerList[0].NickName);
                //topWhiteAvatar.sprite = avatars[(int)PhotonNetwork.PlayerList[0].CustomProperties["avatar"]]);
                //PlayerNameGameWin.text = LimitNameText(PhotonNetwork.PlayerList[0].NickName);
                topBlackNameText.text = PhotonNetwork.PlayerList.Length.ToString();
                topBlackAvatar.sprite = null;
            }
        }

        void StartMessage()
        {
            if (practiceMode)
            {
                startImg.SetActive(true);
                WinImg.SetActive(false);
                loseImg.SetActive(false);
            }
            else if (targetTag == "White")
            {
                startImg.SetActive(true);
                WinImg.SetActive(false);
                loseImg.SetActive(false);
                errorPanel.SetActive(false);
            }
            else if (targetTag == "Black")
            {
                startImg.SetActive(false);
                WinImg.SetActive(false);
                loseImg.SetActive(false);
                errorPanel.SetActive(false);
            }

            statusPanel.anchoredPosition = new Vector2(-940f, statusPanel.anchoredPosition.y);
            statusBackground.color = new Color(0, 0, 0, 0);
            statusScreen.SetActive(true);

            LeanTween.alpha(statusBackground.GetComponent<RectTransform>(), 0.5f, 0.5f);
            LeanTween.move(statusPanel, new Vector2(0, statusPanel.anchoredPosition.y), 0.3f).setDelay(0.5f)
                .setEase(LeanTweenType.easeOutBack).setOnComplete(() =>
                {
                    LeanTween.move(statusPanel, new Vector2(940, statusPanel.anchoredPosition.y), 0.3f).setDelay(1f)
                        .setEase(LeanTweenType.easeInBack).setOnComplete(
                            () => {
                                statusScreen.SetActive(false);
                                startImg.SetActive(false);
                            });
                });
        }

        void Update()
        {
            // Debug.Log($"Linear Damping {PlayerPuck.Instance.rb.linearDamping}");
            if (isSliding && Time.time - lastValueChangeTime > 0.1f)
            {
                isSliding = false;

                // Stop sound locally always
                if (audioSource != null && audioSource.isPlaying)
                {
                    audioSource.Stop();
                }
            }

            if (practiceMode) return;
            if (winnerAnnounced || gameState == GameState.WIN || gameState == GameState.LOSE) return;

            if (isWaitingForTurnStart)
            {
                turnStartTimeout += Time.deltaTime;

                if (turnStartTimeout > TURN_START_MAX_WAIT)
                {
                    Debug.LogError("[RECOVERY] Turn timeout - forcing CheckTurn()");
                    isWaitingForTurnStart = false;
                    allowPocketing = true;
                    CheckTurn();
                }
            }
            RunTimer();
        }

        void FixedUpdate()
        {
            if (gameState == GameState.READY || gameState == GameState.WAIT)
            {
                isPucksFixed = GetPuckStatus();
            }

            if (!isPucksFixed)
            {
                AudioManager.Instance.StopTimeoutSound();
            }
        }

        void RunTimer()
        {
            if (gameState != GameState.READY) return;
            if (winnerAnnounced) return;

            if (reduceTimer)
            {
                timer -= 1 * Time.deltaTime;
            }

            if (PhotonController.Instance.offlineMatch || PhotonNetwork.InRoom)
            {
                // ✅ Check if it's the local player's turn
                bool isMyTurn = (masterClientTag == targetTag);

                if (masterClientTag == "White")
                {
                    topWhiteAvatarTimer.fillAmount = Mathf.InverseLerp(0, Constants.PLAY_TIME_FOR_PLAYER, timer);
                    if (topWhiteAvatarTimer.fillAmount <= 0.24f)
                    {
                        topWhiteAvatarTimer.color = Color.red;
                        if (isMyTurn) AudioManager.Instance.PlayTimeoutSound();
                    }
                    else
                    {
                        topWhiteAvatarTimer.color = Color.green;
                        if (isMyTurn) AudioManager.Instance.StopTimeoutSound();
                    }
                }
                else
                {
                    topBlackAvatarTimer.fillAmount = Mathf.InverseLerp(0, Constants.PLAY_TIME_FOR_PLAYER, timer);
                    if (topBlackAvatarTimer.fillAmount <= 0.24f)
                    {
                        topBlackAvatarTimer.color = Color.red;
                        if (isMyTurn) AudioManager.Instance.PlayTimeoutSound();
                    }
                    else
                    {
                        topBlackAvatarTimer.color = Color.green;
                        if (isMyTurn) AudioManager.Instance.StopTimeoutSound();
                    }
                }
            }

            if (timer <= 0 && playerPhotonView.IsMine)
            {
                gameState = GameState.WAIT;
                PlayerPuck.Instance.isTouch = false;
                PlayerPuck.Instance.arrow.SetActive(false);
                PlayerPuck.Instance.tutorial.SetActive(false);
                PlayerPuck.Instance.tutorialShowDelay = 2f;
                shootAgain = false;

                StartCoroutine(WaitForPucks());
            }
        }

        public void SettingsBtn()
        {
            if (gameState == GameState.WIN || gameState == GameState.LOSE) return;

            settingsContinue.anchoredPosition = new Vector2(-940f, settingsContinue.anchoredPosition.y);
            settingsMenu.anchoredPosition = new Vector2(-940f, settingsMenu.anchoredPosition.y);
            settingsBackground.color = new Color(0, 0, 0, 0);
            settingsScreen.SetActive(true);

            LeanTween.alpha(settingsBackground.GetComponent<RectTransform>(), 0.75f, 0.25f);
            LeanTween.move(settingsContinue, new Vector2(0, settingsContinue.anchoredPosition.y), 0.3f).setDelay(0.25f)
                .setEase(LeanTweenType.easeOutBack);
            LeanTween.move(settingsMenu, new Vector2(0, settingsMenu.anchoredPosition.y), 0.3f).setDelay(0.5f)
                .setEase(LeanTweenType.easeOutBack);
        }

        public void SettingsContinueBtn()
        {
            settingsScreen.SetActive(false);
        }

        public void SettingsMenuBtn()
        {
            settingsContinue.GetComponent<Button>().interactable = false;
            settingsMenu.GetComponent<Button>().interactable = false;

            PhotonNetwork.AutomaticallySyncScene = false;
            //AdsManager.Instance.ShowInterstitialAd();
            PhotonController.Instance.MarkIntentionalLeave();
            if (PhotonNetwork.InRoom)
            {
                PhotonNetwork.LeaveRoom();
            }
        }

        public void Shoot()
        {
            if (winnerAnnounced) return;

            CircleCollider2D.isTrigger = false;
            playerSlider.interactable = false;
            if (opponentSlider != null)
            {
                opponentSlider.interactable = false;
            }

            shootAgain = false;
            gameState = GameState.WAIT;

            playerSlider.value = 0.5f;
            if (opponentSlider != null)
            {
                opponentSlider.value = 0.5f;
            }

            StartCoroutine(WaitForPucks());
        }

        public void NavigatorSliderValueChanged()
        {
            if (!playerPhotonView.IsMine || gameState != GameState.READY) return;
            CircleCollider2D.isTrigger = true;

            if(!PhotonController.Instance.offlineMatch)
            {
                if(PhotonNetwork.LocalPlayer.ActorNumber != 1)
                {
                    playerPuck.transform.localPosition = new Vector2(
                    Mathf.Lerp(playerPuckMaxX, playerPuckMinX, playerSlider.value),
                    playerPuck.transform.localPosition.y
                    );
                }
                else
                {
                    playerPuck.transform.localPosition = new Vector2(
                    Mathf.Lerp(playerPuckMinX, playerPuckMaxX, playerSlider.value),
                    playerPuck.transform.localPosition.y
                    );
                }
            }
            else
            {
                playerPuck.transform.localPosition = new Vector2(
                Mathf.Lerp(playerPuckMinX, playerPuckMaxX, playerSlider.value),
                playerPuck.transform.localPosition.y
                );
            }
            

            if (!isSliding)
            {
                isSliding = true;
                lastValueChangeTime = Time.time;

                // Play sliding sound locally always
                if (audioSource != null && !audioSource.isPlaying)
                {
                    audioSource.loop = true;
                    audioSource.volume = 0.5f; // Adjust volume for better experience
                    audioSource.Play();
                }
            }

            lastValueChangeTime = Time.time;
        }

        bool GetPuckStatus()
        {
            for (int i = 0; i < allPucks.Length; i++)
            {
                if (allPucks[i].isMoving)
                {
                    reduceTimer = false;
                    return false;
                }
            }

            if (PlayerPuck.Instance.isMoving)
            {
                return false;
            }

            return true;
        }

        // --- RED STATE MANAGEMENT (authoritative & synced) -------------------

        // Call this instead of directly assigning redPuckCollected.
        // REPLACED: now uses room property instead of RPC
        private void SetRedPuckCollected(bool value)
        {
            if (!PhotonController.Instance.offlineMatch)
            {
                // Never clear a legitimately collected red during a match
                if (redPuckCollected && value == false) return;

                // 1) Update room property (shared truth)
                SetRedCollectedRoomProperty(value);

                // 2) Also apply immediately locally (UI responsiveness)
                ApplyRedCollectedFromSharedState(value);
            }
            else
            {
                redPuckCollectedLocal = value;
                ApplyRedCollectedFromSharedState(value);
            }
        }

        //[PunRPC]
        //private void SetRedCollectedRPC(bool value)
        //{
        //    redPuckCollected = value;
        //    redPuckWaiting = false;
        //    lastPocketedColor = "";
        //    UpdateRedIcon();
        //}

        // Force red reset only when it was illegally pocketed; never clear a legit collected red.
        // Force red reset only when it was illegally pocketed; never clear a legit collected red.
        public void ResetRedPuckForAllClients()
        {
            // IMPORTANT: use room property as truth (not local flag)
            if (GetRedCollectedFromRoom()) return; // do not reset after legit collection

            Debug.Log($"🔄 ResetRedPuckForAllClients called by [{targetTag}] redCollected(room): {GetRedCollectedFromRoom()}");

            if (redPuck == null) return;

            if (!PhotonController.Instance.offlineMatch)
            {    
                var pv = redPuck.GetComponent<PhotonView>();
                if (pv != null)
                {
                    // This RPC is OK because it's about POSITION reset, not about the "collected" state
                    pv.RPC("ForceResetPositionRPC", RpcTarget.AllBuffered);
                }
                else
                {
                    var puck = redPuck.GetComponent<Puck>();
                    if (puck != null) puck.ResetPosition();
                }
            }
            else
            {
                redPuck.GetComponent<Puck>().ForceResetPositionRPC();
            }

            // Local-only cleanup (DO NOT broadcast flags)
            redPuckWaiting = false;
            lastPocketedColor = "";

            // Ensure UI is consistent with room property
            ApplyRedCollectedFromSharedState(GetRedCollectedFromRoom());
        }

        //[PunRPC]
        //void ClearRedFlagsRPC()
        //{
        //    if (GetRedCollectedFromRoom()) return;// safety: never clear a legit collected red

        //    Debug.Log($"⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️");
        //    Debug.Log($"[{targetTag}] ClearRedFlagsRPC called!");
        //    Debug.Log($"[{targetTag}] BEFORE: redPuckWaiting = {redPuckWaiting}, redPuckCollected = {redPuckCollected}");
        //    Debug.Log($"[{targetTag}] Called from: {System.Environment.StackTrace}");

        //    redPuckWaiting = false;
        //    redPuckCollected = false;
        //    lastPocketedColor = "";
        //    UpdateRedIcon();

        //    Debug.Log($"[{targetTag}] AFTER: redPuckWaiting = {redPuckWaiting}, redPuckCollected = {redPuckCollected}");
        //    Debug.Log($"⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️⚠️");
        //}

        private void ApplyRedCollected(bool value)
        {
            //SetRedCollectedRPC(value);
        }

        private void UpdateRedIcon()
        {
            if (redCoinIcon == null) return;

            // Room property is the truth
            bool collected = GetRedCollectedFromRoom();

            redCoinIcon.SetActive(collected);
        }

        //private void UpdateRedIconEveryTurn()
        //{
        //    if (redCoinIcon != null)
        //    {
        //        // Show icon when red is NOT on board AND redPuckWaiting is false
        //        bool redIsNotOnBoard = !IsRedPuckOnBoard();
        //        bool shouldShowIcon = redIsNotOnBoard && !redPuckWaiting;

        //        redCoinIcon.SetActive(shouldShowIcon);
        //    }
        //}

        // --------------------------------------------------------------------
        // NEW: Room property key for red collected state
        private const string ROOMPROP_RED_COLLECTED = "RedCollected";

        // NEW: Read red collected from room properties (single source of truth)
        private bool GetRedCollectedFromRoom()
        {
            if (PhotonController.Instance.offlineMatch)
            {
                return redPuckCollectedLocal;
            }
            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
                return redPuckCollected; // fallback local

            if (PhotonNetwork.CurrentRoom.CustomProperties != null &&
                PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(ROOMPROP_RED_COLLECTED))
            {
                object v = PhotonNetwork.CurrentRoom.CustomProperties[ROOMPROP_RED_COLLECTED];
                if (v is bool b) return b;
            }

            return false;
        }

        // NEW: Set red collected in room properties (do not clear during match)
        private void SetRedCollectedRoomProperty(bool value)
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
                return;

            // Once true, never set back to false during a match
            if (GetRedCollectedFromRoom() && value == false) return;

            ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
            props[ROOMPROP_RED_COLLECTED] = value;
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        }

        // NEW: Apply red state locally based on a shared value
        private void ApplyRedCollectedFromSharedState(bool value)
        {
            redPuckCollected = value;
            redPuckWaiting = false;
            lastPocketedColor = "";

            // Optional: enforce visual removal if red is collected
            if (value && redPuck != null)
            {
                redPuck.SetActive(false);
            }

            UpdateRedIcon();
        }

        public void CheckTurn()
        {
            ShowDiskAndPucks();

            // Re-enable pocketing when a new turn is prepared
            if (winnerAnnounced) return;

            if (!PhotonController.Instance.offlineMatch && !AllLoaded())
            {
                return;
            }
            allowPocketing = true;
            //AudioManager.Instance.StopTimeoutSound();
            reduceTimer = true;
            if (!isTurnHardRestarting)
            {
                redPuckWaiting = false;
            }
            isTurnHardRestarting = false;
            timer = Constants.PLAY_TIME_FOR_PLAYER;
            topWhiteAvatarTimer.fillAmount = 0;
            topBlackAvatarTimer.fillAmount = 0;

            shooterTagAtTurnStart = masterClientTag;

            PlayerPuck.Instance.DisableCollider();

            if (whichPlayer == WhichPlayer.ME)
            {
                Debug.Log("D: Whichplayer is Me");
                PlayerPuck.Instance.DisableTouchControls();
                PlayerPuck.Instance.ResetPosition();
                playerSliderBG.SetActive(true);
                StartCoroutine(BlinkDisc());

                float alpha = 1f;
                var c = playerSliderBG.GetComponent<Image>().color;
                playerSliderBG.GetComponent<Image>().color = new Color(c.r, c.g, c.b, alpha);

                playerSlider.interactable = true;
                if (opponentSlider != null)
                {
                    //opponentSliderBG.SetActive(true);
                    //float alpha = 0.5f;
                    //var c = opponentSliderBG.GetComponent<Image>().color;
                    //opponentSliderBG.GetComponent<Image>().color = new Color(c.r, c.g, c.b, alpha);
                    //opponentSlider.interactable = false;
                }
            }
            else
            {
                PlayerPuck.Instance.EnableTouchControls();
                Debug.Log("D: Whichplayer is other");
                if (PhotonController.Instance.offlineMatch)
                {
                    PlayerPuck.Instance.ShootBot();
                }
                playerSlider.interactable = false;
                playerSliderBG.SetActive(true);
                float alpha = 0.5f;
                var c = playerSliderBG.GetComponent<Image>().color;
                playerSliderBG.GetComponent<Image>().color = new Color(c.r, c.g, c.b, alpha);

                if (opponentSlider != null)
                {
                    //opponentSlider.interactable = true;
                    //opponentSliderBG.SetActive(true);
                }
            }

            gameState = GameState.READY;

            if (practiceMode)
            {
                allPucks[0].ResetPosition();
            }

            for (int i = 0; i < allPucks.Length; i++)
            {
                allPucks[i].GetComponent<CircleCollider2D>().enabled = true;
            }

            PlayerPuck.Instance.EnablePuckPhysic();
            //CircleCollider2D.isTrigger = false;
            //UpdateRedIconEveryTurn();
        }

        IEnumerator BlinkDisc()
        {
            yield return new WaitForSeconds(0);
            DiscBlinker.SetActive(true);
            yield return new WaitForSeconds(3);
            DiscBlinker.SetActive(false);
        }

        public void PuckOnHole(string puckTag, string puckName)
        {
            // Prevent pocketing during turn-change period
            if (!allowPocketing) return;

            foreach(GameObject puck in homePucksCollected)
            {
                if(puck.name == puckName)
                {
                    return;
                }
            }

            foreach(GameObject puck in awayPucksCollected)
            {
                if(puck.name == puckName)
                {
                    return;
                }
            }

            if (practiceMode) return;
            if (gameState == GameState.WIN || gameState == GameState.LOSE) return;

            if (puckTag == "Player")
            {
                pucksCollected.Add(playerPuck.gameObject);
            }
            else
            {
                Puck getPuck = getPuckFromList(puckName);
                if (getPuck != null)
                {
                    pucksCollected.Add(getPuck.gameObject);
                }
            }


            Debug.Log("---------------HOME------------------");
            foreach(GameObject puck in homePucksCollected)
            {
                Debug.Log($"Pocketed: {puck.name}");
            }
            Debug.Log("---------------AWAY------------------");
            foreach(GameObject puck in awayPucksCollected)
            {
                Debug.Log($"Pocketed: {puck.name}");
            }
            Debug.Log("---------------NOW------------------");
            foreach(GameObject puck in pucksCollected)
            {
                Debug.Log($"Pocketed: {puck.name}");
            }
            Debug.Log("------------------------------------");

        }

        Puck getPuckFromList(string puckName)
        {
            for (int i = 0; i < allPucks.Length; i++)
            {
                if (allPucks[i].name == puckName)
                {
                    return allPucks[i];
                }
            }

            return null;
        }

        IEnumerator WaitForPucks()
        {
            yield return new WaitForSeconds(1f);
            yield return new WaitUntil(() => isPucksFixed);
            yield return new WaitForSeconds(1f);

            PlayerPuck.Instance.DestroyAllBotLines();
            CheckGameStatus();
        }

        void CheckGameStatus()
        {
            if (winnerAnnounced) return;

            if(!AllLoaded() && !PhotonController.Instance.offlineMatch)
            {
                return;
            }

            Debug.Log($"═══════════════════════════════════════");
            Debug.Log($"[{targetTag}] CheckGameStatus called");
            Debug.Log($"[{targetTag}] pucksCollected.Count: {pucksCollected.Count}");
            Debug.Log($"[{targetTag}] redPuckCollected FLAG: {redPuckCollected}");
            Debug.Log($"[{targetTag}] redPuck.activeSelf: {(redPuck != null ? redPuck.activeSelf.ToString() : "null")}");
            Debug.Log($"[{targetTag}] IsRedPuckOnBoard (PHYSICAL): {IsRedPuckOnBoard()}");
            Debug.Log($"[{targetTag}] redPuckWaiting: {redPuckWaiting}");
            Debug.Log($"[{targetTag}] masterClientTag: {masterClientTag}");
            Debug.Log($"[{targetTag}] homePucksCollected: {homePucksCollected.Count}");
            Debug.Log($"[{targetTag}] awayPucksCollected: {awayPucksCollected.Count}");
            Debug.Log($"═══════════════════════════════════════");

            turnCount ++;

            if (PhotonController.Instance.offlineMatch)
            {
                PlayerPuck.Instance.selectedShot = null;
                PlayerPuck.Instance.SetRepelMode(false);
                WhitePuckDamping(false);
                PlayerPuck.Instance.collider.radius = 0.55f;
            }

            isTurnHardRestarting = false;

            if(pucksCollected.Contains(playerPuck.gameObject))
            {
                CheckPlayerPenalty();
            }
            else if (pucksCollected.Contains(redPuck))
            {
                CheckRedPuckPenalty();
            }
            else
            {
                CheckPucks();
            }

            bool redCollectedByRoom = GetRedCollectedFromRoom();
            bool redIsPhysicallyOnBoard = IsRedPuckOnBoard();

            if (!redCollectedByRoom && !redIsPhysicallyOnBoard && !redPuckWaiting)
            {
                SetRedPuckCollected(true);
            }

            UpdateScoreText();

            if (practiceMode)
            {
                CheckTurn();
                return;
            }

            EvaluateWinLose();

            if (gameState == GameState.WIN || gameState == GameState.LOSE)
            {
                allowPocketing = false;
                reduceTimer = false;

                if (PhotonNetwork.InRoom && !practiceMode)
                {
                    if (PhotonNetwork.IsMasterClient)
                    {
                        if (!winnerAnnounced)
                        {
                            winnerAnnounced = true;
                            string winnerTag = (gameState == GameState.WIN) ? targetTag :
                                               (targetTag == "White" ? "Black" : "White");

                            ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
                            props["GameWinner"] = winnerTag;
                            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
                            GameOver();
                        }
                    }
                    else
                    {
                        if (gameState == GameState.LOSE)
                        {
                            if (!winnerAnnounced)
                            {
                                winnerAnnounced = true;
                                GameOver();
                            }
                        }
                    }
                }
                else
                {
                    winnerAnnounced = true;
                    GameOver();
                }

                return;
            }

            reduceTimer = true;

            if (gameState == GameState.WAIT)
            {
                if (!shootAgain)
                {
                    allowPocketing = false;
                    gameState = GameState.SWITCH_MASTER;

                    if (!playerPhotonView.IsMine) return;
                    StartCoroutine(SwitchMasterDelay());
                }
                else
                {
                    timer = Constants.PLAY_TIME_FOR_PLAYER;

                    if (masterClientTag == "White")
                    {
                        topWhiteAvatarTimer.fillAmount = 0;
                        topWhiteAvatarTimer.color = Color.green;
                    }
                    else
                    {
                        topBlackAvatarTimer.fillAmount = 0;
                        topBlackAvatarTimer.color = Color.green;
                    }

                    gameState = GameState.READY;

                    PlayerPuck.Instance.DisableCollider();

                    if (!playerPhotonView.IsMine)
                    {
                        return;
                    }


                   if (whichPlayer == WhichPlayer.OTHER)
                    {
                        if (PhotonController.Instance.offlineMatch)
                        {
                            PlayerPuck.Instance.ShootBot();
                        }
                        playerSlider.interactable = false;
                        // playerSliderBG.SetActive(false);
                        if (opponentSlider != null)
                        {
                            opponentSlider.interactable = true;
                            opponentSliderBG.SetActive(true);
                        }
                    }
                    else
                    {
                        PlayerPuck.Instance.DisableCollider();
                        PlayerPuck.Instance.ResetPosition();
                        playerSlider.interactable = true;
                        playerSliderBG.SetActive(true);
                        if (opponentSlider != null)
                        {
                            opponentSlider.interactable = false;
                            opponentSliderBG.SetActive(false);
                        }
                        PlayerPuck.Instance.EnableCollider();
                    }
                }
            }
            else
            {
                gameState = GameState.READY;
            }
        }

        private void EvaluateWinLose()
        {
            // Carrom mode with red required for win
            if (PhotonController.Instance.whichMode == "carrom")
            {
                // NEW: shared truth from room property
                bool redCollectedByRoom = GetRedCollectedFromRoom();

                // Optional fallback (only if you want it)
                bool redIsPhysicallyOnBoard = IsRedPuckOnBoard();
                bool redCollectedByPhysical = !redIsPhysicallyOnBoard;

                // Prefer room property; physical is only a fallback if property was never set
                bool redIsCollected = redCollectedByRoom || redCollectedByPhysical;

                Debug.Log($"[{targetTag}] EvaluateWinLose - room redCollected: {redCollectedByRoom}");
                Debug.Log($"[{targetTag}] EvaluateWinLose - physical redCollected: {redCollectedByPhysical}");

                if (redIsCollected)
                {
                    // Red collected: whoever finishes their own color wins
                    if (homePucksCollected.Count == 9)
                    {
                        gameState = targetTag == "White" ? GameState.WIN : GameState.LOSE;
                    }
                    else if (awayPucksCollected.Count == 9)
                    {
                        gameState = targetTag == "Black" ? GameState.WIN : GameState.LOSE;
                    }
                }
                else
                {
                    // Red not collected: apply 9th coin loses (per shooter turn)
                    bool homeReached9 = homePucksCollected.Count == 9;
                    bool awayReached9 = awayPucksCollected.Count == 9;

                    if (homeReached9 || awayReached9)
                    {
                        // Shooter at turn start loses
                        if (shooterTagAtTurnStart == "White")
                        {
                            gameState = targetTag == "White" ? GameState.LOSE : GameState.WIN;
                        }
                        else
                        {
                            gameState = targetTag == "Black" ? GameState.LOSE : GameState.WIN;
                        }
                    }
                }
            }
            else // disc mode
            {
                if (masterClientTag == "White")
                {
                    if (homePucksCollected.Count == 9)
                    {
                        gameState = targetTag == "White" ? GameState.WIN : GameState.LOSE;
                    }
                    else if (awayPucksCollected.Count == 9)
                    {
                        gameState = targetTag == "Black" ? GameState.WIN : GameState.LOSE;
                    }
                }
                else
                {
                    if (awayPucksCollected.Count == 9)
                    {
                        gameState = targetTag == "Black" ? GameState.WIN : GameState.LOSE;
                    }
                    else if (homePucksCollected.Count == 9)
                    {
                        gameState = targetTag == "White" ? GameState.WIN : GameState.LOSE;
                    }
                }
            }
        }
        IEnumerator SwitchMasterDelay(float waitTime = 1.5f)
        {
            if (winnerAnnounced) yield break;

            // ✅ Disable pocketing BEFORE any delay
            allowPocketing = false;
            AudioManager.Instance.StopTimeoutSound();
            // ✅ Increased delay to ensure physics fully settles
            yield return new WaitForSeconds(waitTime);

            if (winnerAnnounced || gameState == GameState.WIN || gameState == GameState.LOSE) yield break;

            if (PhotonController.Instance.offlineMatch)
            {
                if (whichPlayer == WhichPlayer.ME)
                {
                    whichPlayer = WhichPlayer.OTHER;
                }
                else
                {
                    whichPlayer = WhichPlayer.ME;
                }

                if (masterClientTag == "White")
                {
                    masterClientTag = "Black";
                }
                else
                {
                    masterClientTag = "White";
                }

                shooterTagAtTurnStart = masterClientTag;

                // ✅ Small additional delay before re-enabling
                yield return new WaitForSeconds(0.2f);
                allowPocketing = true;
                CheckTurn();
            }
            else
            {
                ExitGames.Client.Photon.Hashtable roomProps = new ExitGames.Client.Photon.Hashtable();
                roomProps["intentionalSwitch"] = true;
                PhotonNetwork.CurrentRoom.SetCustomProperties(roomProps);

                // ✅ CRITICAL: Wait for property to sync
                yield return new WaitForSeconds(0.3f);

                // ✅ Enable timeout recovery
                isWaitingForTurnStart = true;
                turnStartTimeout = 0f;

                PhotonNetwork.SetMasterClient(PhotonNetwork.MasterClient.GetNext());
            }
        }

        void CheckPlayerPenalty()
        {
            bool playerPocketed = pucksCollected.Contains(playerPuck.gameObject);

            if (playerPocketed && pucksCollected.Count > 1)
            {
                pucksCollected.Remove(playerPuck.gameObject);

                bool hasOwnColorCoin = false;
                for (int i = 0; i < pucksCollected.Count; i++)
                {
                    GameObject targetPuck = pucksCollected[i];
                    if (targetPuck == null) continue;
                    if (targetPuck == redPuck) continue;
                    if (targetPuck.CompareTag("Player")) continue;

                    if (targetPuck.CompareTag(masterClientTag))
                    {
                        hasOwnColorCoin = true;
                        break;
                    }
                }

                // ✅ NEW LOGIC: If red is waiting and own color coin pocketed, COLLECT red
                // ✅ NEW LOGIC: If red is in list OR waiting, and own color coin pocketed, COLLECT red
                if ((pucksCollected.Contains(redPuck) || redPuckWaiting) && hasOwnColorCoin)
                {
                    // ✅ Always set redPuckCollected when red + own color are pocketed together
                    SetRedPuckCollected(true);
                    AudioManager.Instance.PlayRedCoinPottedSound();

                    if (redPuckWaiting)
                    {
                        redPuckWaiting = false;
                    }

                    pucksCollected.Remove(redPuck);
                    HidePocketedPuck(redPuck);
                }
                // If red is waiting but NO own color coin pocketed, reset red
                else if (pucksCollected.Contains(redPuck) || redPuckWaiting)
                {
                    pucksCollected.Remove(redPuck);
                    ResetRedPuckForAllClients();
                }

                for (int i = 0; i < pucksCollected.Count; i++)
                {
                    GameObject targetPuck = pucksCollected[i];

                    if (targetPuck == null) continue;
                    if (targetPuck.CompareTag("Player")) continue;

                    if (targetPuck.CompareTag("White"))
                    {
                        homePucksCollected.Add(targetPuck);
                    }
                    else if (targetPuck.CompareTag("Black"))
                    {
                        awayPucksCollected.Add(targetPuck);
                    }

                    targetPuck.SetActive(false);
                }

                lastPocketedColor = GetLastPocketedColor(pucksCollected);
                pucksCollected.Clear();
                shootAgain = false;

                // Don't call CheckNinthCoinRule here since red is collected
                return;
            }

            // Handle case where only player puck was pocketed (or player puck + red only)
            if (pucksCollected.Contains(redPuck) || redPuckWaiting)
            {
                pucksCollected.Remove(redPuck);
                ResetRedPuckForAllClients();
            }

            for (int i = 0; i < pucksCollected.Count; i++)
            {
                GameObject targetPuck = pucksCollected[i];

                if (targetPuck == null) continue;
                if (targetPuck.CompareTag("Player")) continue;

                if (targetPuck.CompareTag("White"))
                {
                    homePucksCollected.Add(targetPuck);
                }
                else if (targetPuck.CompareTag("Black"))
                {
                    awayPucksCollected.Add(targetPuck);
                }

                targetPuck.SetActive(false);
            }

            lastPocketedColor = GetLastPocketedColor(pucksCollected);
            pucksCollected.Clear();

            CheckNinthCoinRule();
        }

        void CheckRedPuckPenalty()
        {
            // Capture last pocketed color for this event
            lastPocketedColor = GetLastPocketedColor(pucksCollected);

            // If only red puck was pocketed (count == 1), set it to waiting
            if (pucksCollected.Count == 1)
            {
                redPuckWaiting = true;
                shootAgain = true;
                pucksCollected.Remove(redPuck);
                return;
            }

            // Check what types of coins were pocketed with red
            bool hasOwnColorCoin = false;
            bool hasOpponentColorCoin = false;
            bool hasPlayerPuck = false;
            GameObject lastPocketedCoin = null;

            for (int i = 0; i < pucksCollected.Count; i++)
            {
                GameObject targetPuck = pucksCollected[i];

                if (targetPuck == null) continue;
                if (targetPuck == redPuck) continue;

                // Check for player puck
                if (targetPuck.CompareTag("Player"))
                {
                    hasPlayerPuck = true;
                    continue;
                }

                // Track last non-red, non-player coin
                lastPocketedCoin = targetPuck;

                // Check if this is player's own color
                if (targetPuck.CompareTag(masterClientTag))
                {
                    hasOwnColorCoin = true;
                }
                else if (targetPuck.CompareTag("White") || targetPuck.CompareTag("Black"))
                {
                    hasOpponentColorCoin = true;
                }
            }

            // ✅ RULE: If own color coin is present (even mixed), RED IS COLLECTED
            if (hasOwnColorCoin)
            {
                SetRedPuckCollected(true);
                AudioManager.Instance.PlayRedCoinPottedSound();
                pucksCollected.Remove(redPuck);
                HidePocketedPuck(redPuck);

                // Collect all coins
                for (int i = 0; i < pucksCollected.Count; i++)
                {
                    GameObject targetPuck = pucksCollected[i];

                    if (targetPuck == null) continue;
                    if (targetPuck.CompareTag("Player")) continue;

                    if (targetPuck.CompareTag("White"))
                    {
                        homePucksCollected.Add(targetPuck);
                    }
                    else if (targetPuck.CompareTag("Black"))
                    {
                        awayPucksCollected.Add(targetPuck);
                    }

                    targetPuck.SetActive(false);
                }

                // ✅ Determine shootAgain based on scenario
                if (hasPlayerPuck)
                {
                    // Red + Player + Own → Turn changes (penalty)
                    shootAgain = false;
                }
                else if (hasOpponentColorCoin)
                {
                    // Red + Own + Opponent (Mixed) → Check last coin
                    if (lastPocketedCoin != null && lastPocketedCoin.CompareTag(masterClientTag))
                    {
                        shootAgain = true; // Own color last
                    }
                    else
                    {
                        shootAgain = false; // Opponent color last
                    }
                }
                else
                {
                    // Red + Own only → Shoot again
                    shootAgain = true;
                }

                pucksCollected.Clear();
                redPuckWaiting = false;
                return;
            }

            // ✅ RULE: No own color coin → RED IS RESET
            // This covers: Red + Opponent only, or Red + Player only

            // Reset the red puck
            ResetRedPuckForAllClients();
            pucksCollected.Remove(redPuck);
            redPuckWaiting = false;

            // Collect any coins (opponent coins if present)
            for (int i = 0; i < pucksCollected.Count; i++)
            {
                GameObject targetPuck = pucksCollected[i];

                if (targetPuck == null) continue;
                if (targetPuck.CompareTag("Player")) continue;

                if (targetPuck.CompareTag("White"))
                {
                    homePucksCollected.Add(targetPuck);
                }
                else if (targetPuck.CompareTag("Black"))
                {
                    awayPucksCollected.Add(targetPuck);
                }

                targetPuck.SetActive(false);
            }

            pucksCollected.Clear();
            shootAgain = false; // Turn changes
        }

        void CheckPucks()
        {
            bool tempRedPuckWaiting = redPuckWaiting;

            // NEW: Check if red puck was pocketed simultaneously with other coins
            bool redPocketedNow = pucksCollected.Contains(redPuck);

            if (pucksCollected.Count == 0)
            {
                if (redPuckWaiting)
                {
                    redPuckWaiting = false;
                    ResetRedPuckForAllClients();
                }
                return;
            }

            // NEW: Track what coins were pocketed to determine shootAgain
            bool ownColorPocketed = false;
            bool opponentColorPocketed = false;
            GameObject lastPocketedCoin = null;

            // Find the last pocketed coin (excluding red puck and player puck)
            for (int i = pucksCollected.Count - 1; i >= 0; i--)
            {
                if (pucksCollected[i] != null &&
                    pucksCollected[i] != redPuck &&
                    !pucksCollected[i].CompareTag("Player"))
                {
                    lastPocketedCoin = pucksCollected[i];
                    break;
                }
            }

            for (int i = 0; i < pucksCollected.Count; i++)
            {
                var targetPuck = pucksCollected[i];

                if (targetPuck == null) continue;

                // Skip red puck in this loop - handle it separately
                if (targetPuck == redPuck) continue;

                // Skip player puck
                if (targetPuck.CompareTag("Player")) continue;

                // Track what colors were pocketed
                if (targetPuck.CompareTag(masterClientTag))
                {
                    ownColorPocketed = true;
                }
                else if (targetPuck.CompareTag("White") || targetPuck.CompareTag("Black"))
                {
                    opponentColorPocketed = true;
                }

                if (PhotonController.Instance.whichMode == "carrom")
                {
                    if (masterClientTag == "White")
                    {
                        if (targetPuck.CompareTag("White"))
                        {
                            // NEW: If red was pocketed simultaneously OR was waiting, collect red
                            if (tempRedPuckWaiting || redPocketedNow)
                            {
                                SetRedPuckCollected(true);
                                tempRedPuckWaiting = false;
                                AudioManager.Instance.PlayRedCoinPottedSound();
                                HidePocketedPuck(redPuck);
                            }

                            // Collect the white puck
                            homePucksCollected.Add(targetPuck);
                            HidePocketedPuck(targetPuck);
                        }
                        else  // Black puck (opponent)
                        {
                            // FIXED: If red was waiting and NOT pocketed now, reset ONLY red (not opponent coin)
                            if (tempRedPuckWaiting && !redPocketedNow)
                            {
                                tempRedPuckWaiting = false;
                                ResetRedPuckForAllClients();
                                // Collect opponent coin
                                awayPucksCollected.Add(targetPuck);
                                HidePocketedPuck(targetPuck);
                            }
                            else
                            {
                                awayPucksCollected.Add(targetPuck);
                                HidePocketedPuck(targetPuck);
                            }
                        }
                    }
                    else if (masterClientTag == "Black")
                    {
                        if (targetPuck.CompareTag("Black"))
                        {
                            // NEW: If red was pocketed simultaneously OR was waiting, collect red
                            if (tempRedPuckWaiting || redPocketedNow)
                            {
                                SetRedPuckCollected(true);
                                tempRedPuckWaiting = false;
                                AudioManager.Instance.PlayRedCoinPottedSound();
                                HidePocketedPuck(redPuck);
                            }

                            // Collect the black puck
                            awayPucksCollected.Add(targetPuck);
                            HidePocketedPuck(targetPuck);
                        }
                        else  // White puck (opponent)
                        {
                            // FIXED: If red was waiting and NOT pocketed now, reset ONLY red (not opponent coin)
                            if (tempRedPuckWaiting && !redPocketedNow)
                            {
                                tempRedPuckWaiting = false;
                                ResetRedPuckForAllClients();
                                // Collect opponent coin
                                homePucksCollected.Add(targetPuck);
                                HidePocketedPuck(targetPuck);
                            }
                            else
                            {
                                homePucksCollected.Add(targetPuck);
                                HidePocketedPuck(targetPuck);
                            }
                        }
                    }
                }
                else  // Disc mode
                {
                    if (targetPuck.CompareTag("White"))
                    {
                        homePucksCollected.Add(targetPuck);
                    }
                    else if (targetPuck.CompareTag("Black"))
                    {
                        awayPucksCollected.Add(targetPuck);
                    }

                    targetPuck.SetActive(false);
                }
            }

            // NEW: determine the last pocketed color for this turn (ignore red/player)
            lastPocketedColor = GetLastPocketedColor(pucksCollected);

            // NEW: Determine shootAgain based on what was pocketed
            if (ownColorPocketed && !opponentColorPocketed)
            {
                // Only own color coins pocketed - get another turn
                shootAgain = true;
            }
            else if (ownColorPocketed && opponentColorPocketed)
            {
                // Both colors pocketed - check which one went in last
                if (lastPocketedCoin != null && lastPocketedCoin.CompareTag(masterClientTag))
                {
                    // Own color went in last - get another turn
                    shootAgain = true;
                }
                else
                {
                    // Opponent color went in last - turn changes
                    shootAgain = false;
                }
            }
            else if (!ownColorPocketed && opponentColorPocketed)
            {
                // Only opponent coins pocketed - turn changes
                shootAgain = false;
            }
            else
            {
                // No valid coins pocketed (shouldn't happen, but safety)
                shootAgain = false;
            }

            pucksCollected.Clear();
            redPuckWaiting = tempRedPuckWaiting;

            // Check for 9th coin rule (without red pocketed) - only in carrom mode
            if (PhotonController.Instance.whichMode == "carrom")
            {
                CheckNinthCoinRule();
            }
        }
        private bool IsRedPuckOnBoard()
        {
            if (redPuck == null) return false;

            // Check if red puck is active (visible on board)
            if (!redPuck.activeSelf) return false;

            // Also check it's not in the current collected list
            if (pucksCollected.Contains(redPuck)) return false;

            if (redPuck.GetComponent<Puck>().isPocketed)
            {
                return false;
            }

            return true;
        }
        // NEW METHOD: Check for 9th coin winning/losing conditions when red is not pocketed
        // 9th coin rule: only applies when red is NOT collected (per room property)
        void CheckNinthCoinRule()
        {
            if (PhotonController.Instance.whichMode != "carrom")
                return;

            bool redCollectedByRoom = GetRedCollectedFromRoom();

            Debug.Log($"[{targetTag}] CheckNinthCoinRule - redCollected(room): {redCollectedByRoom} " +
                      $"home={homePucksCollected.Count} away={awayPucksCollected.Count}");

            // If room says red is collected, NEVER apply 9th coin lose rule
            if (redCollectedByRoom)
                return;

            bool redIsPhysicallyOnBoard = IsRedPuckOnBoard();
            if (!redIsPhysicallyOnBoard)
            {
                return;
            }

            // If someone reached 9 while red is not collected -> shooter loses
            bool homeReached9 = homePucksCollected.Count == 9;
            bool awayReached9 = awayPucksCollected.Count == 9;

            if (!homeReached9 && !awayReached9)
                return;

            Debug.Log($"[{targetTag}] Applying 9th coin rule (red NOT collected) shooterTagAtTurnStart={shooterTagAtTurnStart}");

            if (shooterTagAtTurnStart == "White")
                gameState = targetTag == "White" ? GameState.LOSE : GameState.WIN;
            else
                gameState = targetTag == "Black" ? GameState.LOSE : GameState.WIN;

            lastPocketedColor = "";
        }

        // Helper to find the last non-red, non-player coin in the collected list
        private string GetLastPocketedColor(List<GameObject> collected)
        {
            if (collected == null) return "";
            for (int i = collected.Count - 1; i >= 0; i--)
            {
                var go = collected[i];
                if (go == null) continue;
                if (go == redPuck) continue;
                if (go.CompareTag("Player")) continue;
                if (go.CompareTag("White")) return "White";
                if (go.CompareTag("Black")) return "Black";
            }
            return "";
        }

        void UpdateScoreText()
        {
            if (homePucksCollected.Contains(redPuck))
            {
                topWhiteScoreText.text = (homePucksCollected.Count - 1).ToString();
            }
            else
            {
                topWhiteScoreText.text = homePucksCollected.Count.ToString();
            }

            if (awayPucksCollected.Contains(redPuck))
            {
                topBlackScoreText.text = (awayPucksCollected.Count - 1).ToString();
            }
            else
            {
                topBlackScoreText.text = awayPucksCollected.Count.ToString();
            }

            UpdateRedIcon();
        }

        public void CheckRoomPlayers()
        {
            // If we already have a final state or a winner is known, do nothing.
            if (gameState == GameState.WIN || gameState == GameState.LOSE) return;
            if (winnerAnnounced) return;
            if (PhotonNetwork.CurrentRoom != null &&
                PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey("GameWinner"))
            {
                return;
            }

            // Only one player left -> remaining player wins
            if (PhotonNetwork.PlayerList.Length == 1)
            {
                gameState = GameState.WIN;
                reduceTimer = false;
                winnerAnnounced = true;
                GameOver();
            }
        }

        // Add to GameController.cs
        public bool isWaitingForReconnection = false;
        private float reconnectionGraceTime = 20f; // 30 seconds grace period

        public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
        {
            if (gameState == GameState.WIN || gameState == GameState.LOSE) return;
            if (winnerAnnounced) return;

            // Only care if we are truly alone
            if (PhotonNetwork.PlayerList.Length != 1) return;

            // ✅ If match NOT started yet (early leave while loading / before stable 2 players):
            // remaining player must leav e from game (NO waiting, NO win API flow).
            if (!matchStarted)
            {
                HideWaitingForOpponent();
                isWaitingForReconnection = false;

                Debug.Log("[Match] Opponent left before match started -> leaving game for remaining player.");
                LeaveFromGame();
                return;
            }

            // // ✅ If match started: use your existing 15s grace waiting logic
            // if (!isWaitingForReconnection)
            // {
            //     isWaitingForReconnection = true;
            //     ShowWaitingForOpponent();
            //     StartCoroutine(WaitForReconnection());
            // }
        }

        public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
        {
            Debug.Log("D: Player Reconnected");
            // If opponent came back, hide waiting UI and stop waiting state
            if (PhotonNetwork.PlayerList.Length == 2)
            {
                Debug.Log("D: Both players are here");
                HideWaitingForOpponent();
                isWaitingForReconnection = false;

                if (PhotonNetwork.IsMasterClient)
                {
                    PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable
                    {
                        { "playerMissing", null }
                    });
                }
            }
        }


        public IEnumerator WaitForReconnection()
        {
            if (!matchStarted)
            {
                HideWaitingForOpponent();
                isWaitingForReconnection = false;

                if (PhotonNetwork.InRoom && (PhotonNetwork.PlayerList == null || PhotonNetwork.PlayerList.Length < 2))
                {
                    Debug.Log("[Match] Opponent left before match started -> leaving game (no win).");
                    LeaveFromGame();
                }
                yield break;
            }
            float waitTime = 0f;

            Debug.Log("D: 1");
            allowPocketing = false;
            bool tempReduceTimer = reduceTimer;
            reduceTimer = false;
            while (waitTime < reconnectionGraceTime)
            {
                if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("playerMissing", out object val))
                {
                    if(val == null)
                    {
                        HideWaitingForOpponent();
                        isWaitingForReconnection = false;
                        allowPocketing = true;
                        reduceTimer = tempReduceTimer;
                        yield break;
                    }
                }

                yield return new WaitForSeconds(1f);
                waitTime += 1f;
            }
            if (PhotonNetwork.PlayerList.Length == 1)
            {
                int lossCount = 0;
                if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("BetCount", out object val))          
                {
                    lossCount = (int)val;
                }
                Debug.Log("after increment: " + lossCount);
                if (lossCount == 2)
                {
                    gameState = GameState.WIN;
                    reduceTimer = false;
                    winnerAnnounced = true;
                    GameOver();
                }

                else
                {
                    Debug.Log("panel here");
                    LoseAPIErrorPanel.SetActive(true);
                }

            }

            isWaitingForReconnection = false;
        }

        public void LeaveFromGame()
        {
            AudioManager.Instance.StopSFXSound();
            PhotonController.Instance.MarkIntentionalLeave();
            StartCoroutine(LeaveRoomWithDelay());
        }
        IEnumerator LeaveRoomWithDelay()
        {
            yield return new WaitForSeconds(1f);

            if (PhotonNetwork.InRoom)
            {
                PhotonNetwork.LeaveRoom();
            }

            // SceneManager.LoadScene("Menu");
            WebBrowserBridge.Instance.ReloadCurrentPage();
        }

        IEnumerator DelayShareBtn()
        {
            yield return new WaitForSeconds(2f);
            WinBetAnim.SetActive(true);
            NamePanelGameWin.SetActive(true);
            //WinBetAnim.GetComponent<AnimatedNumberTMPro>().PlayRollSound();
            int temp = roomWinPrice - (int)(roomWinPrice * 0.1);
            WinBetAnim.GetComponent<AnimatedNumberTMPro>().target = temp;

            //yield return new WaitForSeconds(1f);
            //WinBetAnim.GetComponent<AnimatedNumberTMPro>().StopSound();
            yield return new WaitForSeconds(6f);
            gameOverBtns[1].SetActive(true);
            gameOverBtns[0].SetActive(true);

        }

        IEnumerator LeaveRoomAfterWinLoss(bool isWin)
        {
            yield return new WaitForSeconds(1f);

            if (isWin)
            {
                yield return StartCoroutine(WaitAndWinCredit());
            }
            Debug.Log($"[CanvasManager] Processing {(isWin ? "WIN" : "LOSS")} - updating stats and coins");

            yield return new WaitForSeconds(2f);

            HomeBtn.SetActive(true);
        }

        void GameOver()
        {
            reduceTimer = false;
            allowPocketing = false;

            AudioManager.Instance.StopTimeoutSound();
            // ✅ Prevent multiple calls
            if (isAllFinished) return;

            Debug.Log($"[GameOver] Called for player {targetTag} with state: {gameState}");

            settingsScreen.SetActive(false);
            gameOverBtns[0].SetActive(false);
            gameOverBtns[1].SetActive(false);

            PhotonNetwork.AutomaticallySyncScene = false;

            if (!string.IsNullOrEmpty(PlayerimageUrl))
                StartCoroutine(LoadImageFromUrl(PlayerimageUrl, PlayerImgGameWin, defaultSprite));
            //PlayerNameGameWin.text = LimitNameText(PhotonNetwork.PlayerList[0].NickName);

            if (gameState == GameState.WIN)
            {
                startImg.SetActive(false);
                Debug.Log($"[GameOver] Showing WIN image for {targetTag}");

                StartCoroutine(LeaveRoomAfterWinLoss(isWin: true));
            }
            else if (gameState == GameState.LOSE)
            {
                startImg.SetActive(false);
                Debug.Log($"[GameOver] Showing LOSE image for {targetTag}");
                AudioManager.Instance.PlayLoseSound();
                ColorUtility.TryParseHtmlString(
                    IsRedPuckOnBoard() ? "#FFFFFF" : "#FF4A4A",
                    out Color color
                );
                redPuckConfirmationImg.color = color;
                loseImg.SetActive(true);
                WinImg.SetActive(false);
                StartCoroutine(LeaveRoomAfterWinLoss(isWin: false));
            }

            statusPanel.anchoredPosition = new Vector2(-940f, statusPanel.anchoredPosition.y);
            statusBackground.color = new Color(0, 0, 0, 0);
            statusScreen.SetActive(true);

            LeanTween.alpha(statusBackground.GetComponent<RectTransform>(), 0.75f, 0.5f);
            LeanTween.move(statusPanel, new Vector2(0, statusPanel.anchoredPosition.y), 0.3f).setDelay(0.5f)
                .setEase(LeanTweenType.easeOutBack).setOnComplete(() =>
                {
                    LeanTween.move(statusPanel, new Vector2(940, statusPanel.anchoredPosition.y), 0.3f).setDelay(1f)
                        .setEase(LeanTweenType.easeInBack).setOnComplete(
                            () =>
                            {
                                statusBackground.color = new Color(0, 0, 0, 0);
                                Vector2 targetPos = topCoinParentForAnim.position;
                                if (gameState == GameState.WIN)
                                {
                                    if (targetTag == "White")
                                    {
                                        targetPos = topWhiteAvatarTimer.transform.position;
                                    }
                                    else
                                    {
                                        targetPos = topBlackAvatarTimer.transform.position;
                                    }
                                }
                                else if (gameState == GameState.LOSE)
                                {
                                    if (targetTag == "White")
                                    {
                                        targetPos = topBlackAvatarTimer.transform.position;
                                    }
                                    else
                                    {
                                        targetPos = topWhiteAvatarTimer.transform.position;
                                    }
                                }

                                topCoinParentForAnim.gameObject.SetActive(true);
                                for (int i = 0; i < topCoinParentForAnim.childCount; i++)
                                {
                                    GameObject go = topCoinParentForAnim.GetChild(i).gameObject;
                                    LeanTween.move(go, targetPos, 0.5f).setDelay(i * 0.1f).setOnComplete(() =>
                                    {
                                        go.SetActive(false);
                                    });
                                }

                                LeanTween.value(PhotonController.Instance.roomEntryPice * 2, 0, 1.5f).setOnUpdate(
                                    (float val) =>
                                    {
                                        if (val < 1)
                                        {
                                            topTotalBetText.text = "0";
                                        }
                                        else
                                        {
                                            topTotalBetText.text = ((int)val).ToString("###,###,###") + "LKR";
                                        }
                                    }).setOnComplete(() =>
                                    {
                                        isAllFinished = true;
                                    });
                            });
                });
        }

        private void HidePocketedPuck(GameObject puck)
        {
            if (puck == null) return;
            puck.SetActive(false);
        }

        public void HideDiskAndPucks()
        {
            disk.SetActive(false);

            for (int i = 0; i < allPucks.Length; i++)
            {
                allPucks[i].gameObject.SetActive(false);
            }
        }

        public void ShowDiskAndPucks()
        {
            disk.SetActive(true);

            for (int i = 0; i < allPucks.Length; i++)
            {
               allPucks[i].gameObject.SetActive(true);
            }
        }

        void CheckInternet()
        {
            StartCoroutine(CheckInternetCoroutine());
        }

        IEnumerator CheckInternetCoroutine()
        {
            if(gameState == GameState.WIN || gameState == GameState.LOSE)
            {
                yield break;
            }

            using (UnityWebRequest request = UnityWebRequest.Get("https://httpbin.org/get"))
            {
                request.timeout = 2;
                yield return request.SendWebRequest();

                bool success = request.result == UnityWebRequest.Result.Success;

                if (success)
                {
                    lastOnlineTime = Time.realtimeSinceStartup;
                    if (reconnectingPanel.activeInHierarchy)
                    {
                        Debug.Log($"Internet Reconnected \n Time - {Time.realtimeSinceStartup}, Last Online Time - {lastOnlineTime}");
                        reconnectingPanel.SetActive(false);
                        Time.timeScale = 1f;
                        ShowDiskAndPucks();
                        if(whichPlayer == WhichPlayer.ME)
                        {
                            StartCoroutine(SwitchMasterDelay(0f));
                        }
                        else
                        {
                            CheckTurn();
                        }
                        if(offlineReconnectFlow != null)
                        {
                            StopCoroutine(offlineReconnectFlow);
                            offlineReconnectFlow = null;
                        }
                    }
                }
                else
                {
                    if (Time.realtimeSinceStartup - lastOnlineTime >= 4f && !reconnectingPanel.activeInHierarchy)
                    {
                        Debug.Log($"Internet Disconnected \n Time - {Time.realtimeSinceStartup}, Last Online Time - {lastOnlineTime}");
                        reconnectingPanel.SetActive(true);
                        Time.timeScale = 0f;
                        HideDiskAndPucks();
                        offlineReconnectFlow = StartCoroutine(OfflineReconnectFlow());
                    }
                }
            }
        }

        IEnumerator OfflineReconnectFlow()
        {
            Debug.Log("Started OfflineReconnectFlow coroutine");
            yield return new WaitForSecondsRealtime(15f);
            reconnectingPanel.SetActive(false);
            gameState = GameState.LOSE;
            ShowDisconnectLose();
        }

        IEnumerator InvokeRepeatingUnscaled(System.Action action, float delay, float interval)
        {
            yield return new WaitForSecondsRealtime(delay);

            while (true)
            {
                action?.Invoke();
                yield return new WaitForSecondsRealtime(interval);
            }
        }

        public void WhitePuckDamping(bool yes)
        {
            foreach(Puck puck in allPucks)
            {
                if (puck.gameObject.CompareTag("White") && !puck.isPocketed)
                {
                    puck.gameObject.GetComponent<Rigidbody2D>().linearDamping = yes? 5f : 2f;
                }
            }
        }
    }

    
}