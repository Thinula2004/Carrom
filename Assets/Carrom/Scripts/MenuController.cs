using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Runtime.InteropServices;
using System;

//game id length = 36
namespace BEKStudio
{
    public class MenuController : MonoBehaviour
    {
        public static MenuController Instance;
        public GameObject dontDestroyPrefab;
        public TextMeshProUGUI coinText;
        public Image avatarImg;
        public TextMeshProUGUI avatarUsernameText;
        public Sprite[] avatars;
        public IEnumerator noOpponentFound;
        IEnumerator OpponentFound;
        IEnumerator OpponentJoined;
        [Header("Main")] public GameObject mainScreen;
        public RectTransform mainDiscPoolRect;
        public RectTransform mainCarromRect;
        public RectTransform mainPracticeRect;
        [Header("Room")] public GameObject roomScreen;
        public TextMeshProUGUI roomGalleEntryPriceText;
        public TextMeshProUGUI roomKandyEntryPriceText;
        public TextMeshProUGUI roomColomboEntryPriceText;
        public TextMeshProUGUI roomJaffnaEntryPriceText;
        public TextMeshProUGUI roomSigiriEntryPriceText;

        public TextMeshProUGUI roomGalleWinPriceText;
        public TextMeshProUGUI roomKandyWinPriceText;
        public TextMeshProUGUI roomColomboWinPriceText;
        public TextMeshProUGUI roomJaffnaWinPriceText;
        public TextMeshProUGUI roomSigiriWinPriceText;
        //public TextMeshProUGUI roomBerlinWinPriceText;
        [Header("Vs")] public GameObject vsScreen;
        public TextMeshProUGUI vsMsgText;
        public Button vsBackBtn;
        public GameObject vsUsersParent;
        public TextMeshProUGUI vsHomeUsernameText;
        public TextMeshProUGUI vsHomeUserBetText;
        public TextMeshProUGUI vsAwayUsernameText;
        public TextMeshProUGUI vsAwayUserBetText;
        public TextMeshProUGUI vsTotalBetText;
        [Header("Username")] public GameObject usernameScreen;
        public TMP_InputField usernameText;

        [Header("Shop")] public GameObject shopScreen;

        [Header("No Opponent")] public GameObject NoOpponentScreen;
        public GameObject waitingPanel;

        [Header("Loading Screen")] public GameObject LoadingScreen;
        public Slider loadingBar;

        public UserData UMdata;
        public UserData userDatas;

        public Image profileImg;
        public Image PlayerImg;
        public Sprite sampleProfile;
        public Image OpponentImg;

        [Header("FindAnims")]

        public int OpponentS;
        public Animator dp;
        public Animator coinflow;
        public Animator coinflow1;
        public Animator numlaod;
        public Animator nameload;
        public Animator coinBonus;

        private string apiUrl = "https://api.playport.lk/api/v1/users/profile";
        //private const string ROOMPROP_BET_COUNT = "BetCount";
        //coin API
        int tempCoin;

        //coin balance
        public GameObject coinErrorPanel;
        public Text coinErrorText;
        public Button ErrorPanelHideBtn;
        public GameObject loadingPanel, versionIssuePanel;
        public TMP_Text versionTxt;

        public List<Sprite> demoProfiles1;
        public List<Sprite> demoProfiles2;

        public GameObject friendsPanel;
        public TMP_Text friendCodeTxt;
        public string tempFriendCode;
        public TMP_Text friendAmountTxt;

        public string user_token;

         #if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void OpenUrlInSameTab(string url);
        [DllImport("__Internal")]
        private static extern void OpenUrlReplace(string url);
        [DllImport("__Internal")]
        private static extern void OpenUrlInSelf(string url);
    #endif


        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            ClearAllPrefs();
        }

        public void ClearAllPrefs()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("✅ All PlayerPrefs have been cleared!");
        }
        
        private string LimitNameText(string fullName, int maxLength = 8)
        {
            if (string.IsNullOrEmpty(fullName)) return "";
            return fullName.Length > maxLength ? fullName.Substring(0, maxLength) + ".." : fullName;
        }

        // Add this helper method anywhere in the MenuController class
        private string FormatCurrency(int amount)
        {
            if (amount >= 1000000)
            {
                // For millions: 1.5M, 2M, etc.
                return (amount / 1000000f).ToString("0.#") + "M";
            }
            else if (amount >= 1000)
            {
                // For thousands: 1k, 1.2k, 15k, etc.
                return (amount / 1000f).ToString("0.#") + "k";
            }
            else
            {
                // Below 1000: show as-is
                return amount.ToString();
            }
        }
        
       void Start()
        {
            friendsPanel.SetActive(false);
            tempFriendCode = null;
            friendCodeTxt.text = "";
            versionIssuePanel.SetActive(false);
            versionTxt.text = VersionChecker.GetVersion();
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Application.targetFrameRate = 60;
            AudioManager.Instance.StopRollingSound();
            AudioManager.Instance.StopTimeoutSound();
            
            // ✅ Extract token FIRST before anything else
            string token = GetTokenFromURL();
            if (!string.IsNullOrEmpty(token))
            {
                AUTH_TOKEN = token;
                Debug.Log("Token successfully retrieved and stored");
            }
            else
            {
                Debug.LogError("Failed to retrieve token from URL");
            }
            
            // ✅ NOW start loading screen (which will use the token)
            ShowLoadingScreen();

            GameObject dontDestroyObj = GameObject.Find("DontDestroy");
            if (dontDestroyObj == null)
            {
                dontDestroyObj = Instantiate(dontDestroyPrefab);
                dontDestroyObj.name = "DontDestroy";
                DontDestroyOnLoad(dontDestroyObj);
            }

            // ResetBotSettings();

            mainScreen.SetActive(false);
            roomScreen.SetActive(false);
            vsScreen.SetActive(false);
            shopScreen.SetActive(false);
            usernameScreen.SetActive(false);

            ShowRooms();
            PhotonController.Instance.offlineMatch = false;
            PhotonController.Instance.demoPlayer = null;
       }

        void CheckVersion()
        {
            StartCoroutine(VersionChecker.CheckVersion(isSame =>
            {
                if (isSame)
                {
                    loadingPanel.SetActive(false);
                }
                else
                {
                    loadingPanel.SetActive(false);
                    versionIssuePanel.SetActive(true);
                }
            }));
        }

        public void ShowLoadingScreen()
        {
            coinErrorPanel.SetActive(false);
            LoadingScreen.SetActive(true);
            loadingBar.value = 0;

            // Start the loading process
            StartCoroutine(LoadingProcess());
        }

        public void ReloadScene()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void CloseCoinErrorPanel()
        {
            coinErrorPanel.SetActive(false);
        }

        public void OpenFriendsPanel(int roomPrice)
        {
            
            string msg = "Your account balance is low!\r\nPlease top up your account\r\nto continue playing";
            int coinBal = UMdata.total_coins;

            if (coinBal <= roomPrice)
            {
                CheckCoinBalance(msg);
                return;
            }
            tempFriendCode = "";
            for (int i = 0; i < 6; i++)
            {
                tempFriendCode += (char)UnityEngine.Random.Range('A', 'Z' + 1);
            }

            friendCodeTxt.text = tempFriendCode;
            friendsPanel.SetActive(true);

            friendAmountTxt.text = "100 LKR";
        }

        public void ChangeBetAmount(bool increase)
        {
            int[] levels = { 100, 200, 500};

            string text = friendAmountTxt.text;
            string amountOnly = text.Replace(" LKR", "").Trim();

            if (!int.TryParse(amountOnly, out int currentAmount))
            {
                Debug.LogWarning("Invalid amount text: " + text);
                return;
            }

            int currentIndex = System.Array.IndexOf(levels, currentAmount);

            if (currentIndex == -1)
            {
                Debug.LogWarning("Amount is not in level list: " + currentAmount);
                return;
            }

            int newIndex = increase ? currentIndex + 1 : currentIndex - 1;

            if (newIndex < 0 || newIndex >= levels.Length)
            {
                return;
            }

            friendAmountTxt.text = levels[newIndex] + " LKR";
        }

        public void CopyFriendCode()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebBrowserBridge.Instance.CopyText(tempFriendCode);
#else
            GUIUtility.systemCopyBuffer = tempFriendCode;
#endif
        }

        public void closeFriendsPanel()
        {
            friendsPanel.SetActive(false);
        }

        public void OnCustomRoomCreate()
        {

            string val = friendAmountTxt.text;
            string friendCode = friendCodeTxt.text;
            int coinsNeeded = 0;

            string whichRoom = "galle"; 

            if(val == "100 LKR")
            {

                whichRoom = "galle";
                coinsNeeded = 100;
                PhotonController.Instance.roomEntryPice = Constants.ROOM_GALLE_ENTRY_PRICE;

            }else if(val == "200 LKR")
            {

                whichRoom = "kandy";
                coinsNeeded = 200;
                PhotonController.Instance.roomEntryPice = Constants.ROOM_COLOMBO_ENTRY_PRICE;

            }else if(val == "500 LKR")
            {
                whichRoom = "colombo";
                coinsNeeded = 5000000;
                PhotonController.Instance.roomEntryPice = Constants.ROOM_KANDY_ENTRY_PRICE;
            }

            string msg = "Your account balance is low!\r\nPlease top up your account\r\nto continue playing";
            int coinBal = UMdata.total_coins;

            if (coinBal <  coinsNeeded)
            {
                CheckCoinBalance(msg);
            }

            Debug.Log($"Which Room : {whichRoom} Friend Code: {friendCode}");
            PhotonController.Instance.whichRoom = whichRoom;
            StartCoroutine(PostGameStatsCoroutine(whichRoom));
            setupScreens();
            PlayerPrefs.SetString("roomName", whichRoom);
            VsJoinedRoom();
            PhotonController.Instance.CreateFriendRoom(friendCode);
        }

        private IEnumerator LoadingProcess()
        {
            bool photonReady = false;
            bool userDataReady = false;
            float loadingProgress = 0f;
            float maxLoadingTime = 25f; // Maximum loading time
            float timer = 0f;

            // Start both processes concurrently
            StartCoroutine(WaitForPhotonConnection(() => photonReady = true));
            StartCoroutine(WaitForUserData(() => userDataReady = true));

            // Update loading bar while waiting
            while (!photonReady || !userDataReady)
            {
                timer += Time.deltaTime;

                // Check for timeout
                if (timer >= maxLoadingTime)
                {
                    Debug.Log("Loading timeout - Connection failed");
                    string msg = "No Connection! Please check your internet and try again.";
                    CheckConnection(msg);
                    LoadingScreen.SetActive(false);
                    yield break;
                }

                // Calculate progress based on completion status
                float targetProgress = 0f;
                if (photonReady) targetProgress += 0.5f;
                if (userDataReady) targetProgress += 0.5f;

                // Smooth progress bar animation
                loadingProgress = Mathf.Lerp(loadingProgress, targetProgress, Time.deltaTime * 2f);
                loadingBar.value = loadingProgress;

                yield return null;
            }

            // Final animation to complete
            while (loadingBar.value < 0.99f)
            {
                loadingBar.value = Mathf.Lerp(loadingBar.value, 1f, Time.deltaTime * 5f);
                yield return null;
            }

            // Small delay before hiding loading screen
            yield return new WaitForSeconds(0.5f);
            LoadingScreen.SetActive(false);
            loadingPanel.SetActive(true);
            CheckVersion(); 
            AudioManager.Instance.StopRollingSound();
        }

        private IEnumerator WaitForPhotonConnection(System.Action onComplete)
        {
            // Wait until Photon is connected to Master Server and joined lobby
            while (!PhotonNetwork.IsConnectedAndReady ||
                !PhotonNetwork.InLobby ||
                PhotonNetwork.Server != Photon.Realtime.ServerConnection.MasterServer)
            {
                // Additional check to ensure we're in the right state for matchmaking
                if (PhotonNetwork.IsConnected &&
                    PhotonNetwork.Server != Photon.Realtime.ServerConnection.MasterServer)
                {
                    //Debug.Log("Waiting for Master Server connection...");
                }
                else if (PhotonNetwork.IsConnectedAndReady && !PhotonNetwork.InLobby)
                {
                    //Debug.Log("Waiting for lobby join...");
                }

                yield return new WaitForSeconds(0.1f);
            }

            Debug.Log("Photon ready for matchmaking - Connected to Master Server and in Lobby");
            onComplete?.Invoke();
        }

        // Centralized method to handle 401 redirects
        private void Handle401Unauthorized()
        {
            Debug.LogWarning("[APIManager] 401 Unauthorized - Redirecting to login page");

#if UNITY_WEBGL && !UNITY_EDITOR
                Application.ExternalEval("window.location.replace('https://playport.lk');");
#else
            Application.OpenURL("https://playport.lk");
#endif
        }

        public void GoWallet()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
                Application.ExternalEval("window.location.replace('https://playport.lk');");
#else
            Application.OpenURL("https://playport.lk");
#endif
        }

        // Helper method to check response code
        private bool CheckForUnauthorized(UnityWebRequest request)
        {
            if (request.responseCode == 401)
            {
                Handle401Unauthorized();
                return true;
            }
            return false;
        }

        public IEnumerator LoadProfileImage(string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl))
            {
                Debug.Log("Profile image URL is empty");
                yield break;
            }

            Debug.Log("Loading profile image from: " + imageUrl);

            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.ConnectionError ||
                    request.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError("Error loading profile image: " + request.error);
                    profileImg.sprite = sampleProfile;
                    PlayerImg.sprite = sampleProfile;
                }
                else
                {
                    // Get the texture from the request
                    Texture2D texture = DownloadHandlerTexture.GetContent(request);

                    // Create a sprite from the texture
                    Sprite sprite = Sprite.Create(
                        texture,
                        new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f)
                    );

                    // Assign to the Image component
                    profileImg.sprite = sprite;
                    PlayerImg.sprite = sprite;

                    Debug.Log("Profile image loaded successfully");
                }
            }
        }

        public IEnumerator LoadOpponentImage(string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl))
            {
                Debug.LogWarning("[OpponentImage] URL is empty.");
                OpponentImg.sprite = sampleProfile;
                yield break;
            }

            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl))
            {
                yield return request.SendWebRequest();
     
                if (request.result == UnityWebRequest.Result.ConnectionError ||
                    request.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError($"[OpponentImage] Error loading opponent image: {request.error}");
                    OpponentImg.sprite = sampleProfile;
                    yield break;
                }

                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                Sprite sprite = Sprite.Create(texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f));

                if (OpponentImg)
                {
                    OpponentImg.sprite = sprite;
                    Debug.Log("[OpponentImage] Opponent image loaded successfully.");
                }
            }
        }

        private IEnumerator WaitForUserData(System.Action onComplete)
        {
            // Start fetching user profile
            yield return StartCoroutine(GetUserProfileCoroutine());

            // Ensure UMdata is set (fallback to default if API fails)
            if (UMdata == null || string.IsNullOrEmpty(UMdata.username))
            {
                UMdata = userDatas; // Use your fallback data
                if (string.IsNullOrEmpty(UMdata.username))
                {
                    UMdata.username = "Guest"; // Final fallback
                }
            }

            onComplete?.Invoke();
        }

        public void GetUserProfile()
        {
            Debug.Log("Fetching user profile...");
            StartCoroutine(GetUserProfileCoroutine());
        }

        //API calling
        [System.Serializable]
        public class GameStatsRequest
        {
            public string game_id;
            public string room_name;
        }

        [System.Serializable]
        public class GameStatsResponse
        {
            public bool success;
            public string message;
            // Add any data fields the API returns if needed
        }
        public IEnumerator PostGameStatsCoroutine(string roomName)
        {
            // Validate required data
            if (string.IsNullOrEmpty(AUTH_TOKEN))
            {
                Debug.LogError("[PostGameStats] AUTH_TOKEN is empty! Cannot post game stats.");
                yield break;
            }

            if (string.IsNullOrEmpty(GAME_ID))
            {
                Debug.LogError("[PostGameStats] GAME_ID is empty! Cannot post game stats.");
                yield break;
            }

            // Create request body
            var statsRequest = new GameStatsRequest
            {
                game_id = GAME_ID,
                room_name = roomName.ToUpper() // Ensure uppercase like "GALLE"
            };

            string json = JsonUtility.ToJson(statsRequest);
            Debug.Log("[PostGameStats] Sending: " + json);

            using (var request = new UnityWebRequest("https://api.playport.lk/api/v1/games-stats", "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();

                request.SetRequestHeader("Authorization", "Bearer " + AUTH_TOKEN);
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                // Check for 401 Unauthorized
                if (CheckForUnauthorized(request))
                {
                    yield break;
                }

                if (request.result == UnityWebRequest.Result.ConnectionError ||
                    request.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError($"[PostGameStats] Error: {request.error} (HTTP {request.responseCode})");
                    Debug.LogError($"[PostGameStats] Response: {request.downloadHandler.text}");
                    yield break;
                }

                string jsonResponse = request.downloadHandler.text;
                Debug.Log("[PostGameStats] Success! Response: " + jsonResponse);

                GameStatsResponse response = JsonUtility.FromJson<GameStatsResponse>(jsonResponse);
                if (response != null && response.success)
                {
                    Debug.Log("[PostGameStats] Game stats posted successfully: " + response.message);
                }
            }
        }

        public void clickRoom()
        {
            StartCoroutine(PostGameStatsCoroutine(PhotonController.Instance.whichRoom));
        }
        public string GAME_ID
        {
            get
            {
                if (string.IsNullOrEmpty(_gameId))
                {
                    _gameId = PlayerPrefs.GetString("GAME_ID", "");
                }
                return _gameId;
            }
            set
            {
                _gameId = value;
                if (!string.IsNullOrEmpty(value))
                {
                    PlayerPrefs.SetString("GAME_ID", value);
                    PlayerPrefs.Save();
                    Debug.Log("GAME_ID saved to PlayerPrefs: " + value);
                }
            }
        }
        private string _gameId = "";

        public string AUTH_TOKEN
        {
            get
            {
                if (string.IsNullOrEmpty(_authToken))
                {
                    _authToken = PlayerPrefs.GetString("AUTH_TOKEN", "");
                }
                return _authToken;
            }
            set
            {
                _authToken = value;
                if (!string.IsNullOrEmpty(value))
                {
                    PlayerPrefs.SetString("AUTH_TOKEN", value);
                    PlayerPrefs.Save();
                    Debug.Log("AUTH_TOKEN saved to PlayerPrefs");
                }
            }
        }
        private string _authToken = "";



   

        [System.Serializable]
        public class UpdateProfileRequest
        {
            public int total_coins;
        }
        
        public IEnumerator UpdateUserProfileCoroutine_PUT(int newTotalCoins)
        {
            // Validate AUTH_TOKEN
            if (string.IsNullOrEmpty(AUTH_TOKEN))
            {
                Debug.LogError("[UpdateUserProfile:PUT] AUTH_TOKEN is empty! Cannot update profile.");
                yield break;
            }

            var update = new UpdateProfileRequest { total_coins = newTotalCoins };
            string json = JsonUtility.ToJson(update);

            using (var request = UnityWebRequest.Put("https://api.playport.lk/api/v1/users/profile", json))
            {
                request.SetRequestHeader("Authorization", "Bearer " + AUTH_TOKEN);
                request.SetRequestHeader("Content-Type", "application/json");
                request.downloadHandler = new DownloadHandlerBuffer();

                yield return request.SendWebRequest();
                
                if (CheckForUnauthorized(request))
                {
                    yield break;
                }
                
                if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError($"[UpdateUserProfile:PUT] Error: {request.error} (HTTP {request.responseCode})");
                    Debug.LogError($"[UpdateUserProfile:PUT] Body: {request.downloadHandler.text}");
                    yield break;
                }

                string jsonResponse = request.downloadHandler.text;
                Debug.Log("[UpdateUserProfile:PUT] Raw Response: " + jsonResponse);

                ApiResponse response = JsonUtility.FromJson<ApiResponse>(jsonResponse);
                if (response != null && response.success)
                {
                    Debug.Log("[UpdateUserProfile:PUT] Success! Message: " + response.message);
                    Debug.Log("[UpdateUserProfile:PUT] New Total Coins: " + response.data.total_coins);
                    OnProfileFetched(response.data);
                }
                else
                {
                    Debug.LogWarning("[UpdateUserProfile:PUT] API reported success=false");
                }
            }

            UpdateCurrencyText();
        }


public IEnumerator GetUserProfileCoroutine()
{
    // Validate token exists
    if (string.IsNullOrEmpty(AUTH_TOKEN))
    {
        Debug.LogError("AUTH_TOKEN is empty! Cannot fetch user profile.");
        UMdata = userDatas;
        avatarUsernameText.text = LimitNameText("Guest");
        yield break;
    }

    using (UnityWebRequest request = UnityWebRequest.Get(apiUrl))
    {
        // Add Bearer token authorization
        request.SetRequestHeader("Authorization", "Bearer " + AUTH_TOKEN);
        Debug.Log("Sending request with Bearer token to: " + apiUrl);
        
        // Send the request
        yield return request.SendWebRequest();
        
        // Check for 401 Unauthorized
        if (CheckForUnauthorized(request))
        {
            yield break;
        }
        

        // Check for errors
        if (request.result == UnityWebRequest.Result.ConnectionError ||
            request.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError("Error: " + request.error);
            Debug.LogError("Response Code: " + request.responseCode);
            Debug.LogError("MSG: " + request.downloadHandler.text);
            
            // Fallback to default user data
            UMdata = userDatas;
            avatarUsernameText.text = LimitNameText("Guest");
        }
        else
        {
            // Success - parse the response
            string jsonResponse = request.downloadHandler.text;
            Debug.Log("Raw Response: " + jsonResponse);

            // Parse the API response
            ApiResponse response = JsonUtility.FromJson<ApiResponse>(jsonResponse);

            if (response != null && response.success)
            {
                Debug.Log("Success! Message: " + response.message);
                Debug.Log("Username: " + response.data.username);
                Debug.Log("Phone: " + response.data.phone_number);
                Debug.Log("Total Coins: " + response.data.total_coins);
                Debug.Log("Account Status: " + response.data.status);
                Debug.Log("KYC Status: " + response.data.kyc_status);
                Debug.Log("Win Percentage: " + response.data.win_percentage + "%");
                Debug.Log("Player Rank: " + response.data.player_rank);
                Debug.Log("IP Address: " + response.data.ip_address);
                
                // Use the data
                OnProfileFetched(response.data);
            }
            else
            {
                Debug.LogWarning("API returned success=false");
                UMdata = userDatas;
                avatarUsernameText.text = LimitNameText("Guest");
            }
        }
    }
}

private void OnProfileFetched(UserData userData)
{
    // Handle the fetched user data here
    UMdata = userData;
    Debug.Log("Profile fetched and stored.");
    Debug.Log($"Username: {UMdata.username}");
    Debug.Log($"Total Coins: {UMdata.total_coins}");
    Debug.Log($"Win Percentage: {UMdata.win_percentage}%");
    Debug.Log($"Player Rank: {UMdata.player_rank}");
    Debug.Log($"IP Address: {UMdata.ip_address}");
    
    avatarUsernameText.text = LimitNameText(UMdata.username);
    PlayerPrefs.SetString("username", avatarUsernameText.text);

    tempCoin = UMdata.total_coins;
     
    PhotonNetwork.NickName = UMdata.username;

    if (!string.IsNullOrEmpty(userData.profile_image_url))
    {
        PlayerPrefs.SetString("profileimage", userData.profile_image_url);
        StartCoroutine(LoadProfileImage(userData.profile_image_url));
    }
    
    // Store additional player data in Photon custom properties
    ExitGames.Client.Photon.Hashtable playerData = new ExitGames.Client.Photon.Hashtable();
    playerData["profile_image_url"] = userData.profile_image_url;
    playerData["win_percentage"] = userData.win_percentage;
    playerData["player_rank"] = userData.player_rank;
    PhotonNetwork.LocalPlayer.SetCustomProperties(playerData);

    UpdateCurrencyText();
}

        // API Response structure
        [System.Serializable]
        public class ApiResponse
        {
            public bool success;
            public string message;
            public UserData data;
        }

        [System.Serializable]
public class UserData
{
    public string id;
    public string username;
    public string phone_number;
    public string email;
    public string password_hash;
    public int total_coins;
    public string profile_image_url;
    public bool is_active;
    public bool is_verified;
    public string status;
    public string suspension_reason;
    public string suspended_until;
    public string kyc_status;
    public string kyc_documents;
    public int risk_score;
    public string created_at;
    public string updated_at;
    public string last_login_at;
    public string suspended_by;
    public string game_id;
    public string ip_address;
    public float win_percentage;  // Use float for percentage values
    public string player_rank;
}

        public string GetTokenFromURL()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string url = Application.absoluteURL;
            Debug.Log("Full URL: " + url);
            
            if (!string.IsNullOrEmpty(url))
            {
                try
                {
                    int queryStart = url.IndexOf('?');
                    if (queryStart >= 0)
                    {
                        string queryString = url.Substring(queryStart + 1);
                        Debug.Log("Query string: " + queryString);
                        
                        string[] parameters = queryString.Split('&');
                        string token = null;
                        
                        foreach (string param in parameters)
                        {
                            Debug.Log("Processing parameter: " + param);
                            
                            int equalIndex = param.IndexOf('=');
                            if (equalIndex > 0)
                            {
                                string key = param.Substring(0, equalIndex);
                                string value = param.Substring(equalIndex + 1);
                                
                                // Decode the URL-encoded value
                                value = UnityEngine.Networking.UnityWebRequest.UnEscapeURL(value);
                                
                                // Check if this is the gameid parameter (with combined game_id + token)
                                if (key == "gameid" || key == "gameId" || key == "game_id")
                                {
                                    Debug.Log("Found gameid parameter with value length: " + value.Length);
                                    
                                    // Validate that the value is long enough to contain both game ID and token
                                    if (value.Length > 36)
                                    {
                                        try
                                        {
                                            // Extract Game ID (first 36 characters - UUID format)
                                            GAME_ID = value.Substring(0, 36);
                                            Debug.Log("Extracted Game ID: " + GAME_ID);
                                            
                                            // Extract Token (everything after the first 36 characters)
                                            token = value.Substring(36);
                                            Debug.Log("Extracted Token (first 20 chars): " + token.Substring(0, Mathf.Min(20, token.Length)) + "...");
                                        }
                                        catch (System.Exception ex)
                                        {
                                            Debug.LogError("Error extracting Game ID and Token: " + ex.Message);
                                        }
                                    }
                                    else
                                    {
                                        Debug.LogError("gameid parameter value is too short. Expected > 36 characters, got: " + value.Length);
                                    }
                                }
                            }
                        }
                        
                        if (token != null && !string.IsNullOrEmpty(GAME_ID))
                        {
                            Debug.Log("Successfully extracted both Game ID and Token");
                            return token;
                        }
                        else if (string.IsNullOrEmpty(GAME_ID))
                        {
                            Debug.LogWarning("Game ID not extracted from URL");
                        }
                        else
                        {
                            Debug.LogWarning("Token not extracted from URL");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("No query string found in URL");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError("Error parsing URL: " + e.Message);
                }
            }
            else
            {
                Debug.LogWarning("Application.absoluteURL is null or empty");
            }
#else
            // Editor mode - use test values
            Debug.Log("Not running in WebGL build - using test values");
            GAME_ID = "3a722139-cb47-46de-a8f6-2e2d041361f5";
            Debug.Log("Editor mode - GAME_ID set to: " + GAME_ID);
            return user_token;
#endif
            
            Debug.LogError("Failed to extract token and game ID from URL");
            return null;
        }

        void ResetBotSettings()
        {
            PhotonController.Instance.playWithBot = false;
            PhotonController.Instance.botAvatar = 0;
            PhotonController.Instance.botName = "";
        }

        public void UpdateCurrencyText()
        {
            if(UMdata.total_coins == 0)
            {
                coinText.text = "0";
            }
            else
            {
                coinText.text = UMdata.total_coins.ToString();
            }
        }

        public void FreeCoinsBtn()
        {
            AdsManager.Instance.ShowRewardedAd();
        }

        public void DiscPoolBtn()
        {
            if (!PhotonNetwork.IsConnectedAndReady) return;

            PhotonController.Instance.whichMode = "disc";
            ShowRooms();
        }

        public void CarromBtn()
        {
            if (!PhotonNetwork.IsConnectedAndReady) return;

            PhotonController.Instance.whichMode = "carrom";
            ShowRooms();
            Debug.Log("CarromBtn");
        }

        public void PracticeModeBtn()
        {
            if (!PhotonNetwork.IsConnectedAndReady) return;

            PhotonController.Instance.whichMode = "practice";
            PhotonController.Instance.CreatePracticeRoom();
        }

        public void ShopBtn()
        {
            Debug.Log("=== GAME_ID DEBUG ===");
            Debug.Log("Current GAME_ID: '" + GAME_ID + "'");

            if (string.IsNullOrEmpty(GAME_ID))
            {
                Debug.LogError("GAME_ID is empty! Check your URL format.");
                Debug.LogError("URL should be: ?gameid=XXXYYY where XXX is 36-char game ID and YYY is token");
                return;
            }

  
        }

        public void ShopItemBtn(int id)
        {
            Purchaser.Instance.BuyConsumable(id);
        }

        public void ShopBackBtn()
        {
            roomScreen.SetActive(true);
            shopScreen.SetActive(false);
        }

        public void ShowRooms()
        {
            roomGalleEntryPriceText.text = "Entry Fee: " + Constants.ROOM_GALLE_ENTRY_PRICE.ToString() + "LKR";
            roomKandyEntryPriceText.text = "Entry Fee: " + Constants.ROOM_KANDY_ENTRY_PRICE.ToString() + "LKR";
            roomColomboEntryPriceText.text = "Entry Fee: " + Constants.ROOM_COLOMBO_ENTRY_PRICE.ToString() + "LKR";
            roomJaffnaEntryPriceText.text = "Entry Fee: " + Constants.ROOM_JAFFNA_ENTRY_PRICE.ToString() + "LKR";
            roomSigiriEntryPriceText.text = "Entry Fee: " + Constants.ROOM_SIGIRI_ENTRY_PRICE.ToString() + "LKR";

            roomGalleWinPriceText.text = Constants.ROOM_GALLE_WIN_PRICE.ToString() + "LKR";
            roomKandyWinPriceText.text = Constants.ROOM_KANDY_WIN_PRICE.ToString() + "LKR";
            roomColomboWinPriceText.text = Constants.ROOM_COLOMBO_WIN_PRICE.ToString() + "LKR";
            roomJaffnaWinPriceText.text = Constants.ROOM_JAFFNA_WIN_PRICE.ToString() + "LKR";
            roomSigiriWinPriceText.text = Constants.ROOM_SIGIRI_WIN_PRICE.ToString() + "LKR";

            PhotonController.Instance.whichMode = "carrom";
            Debug.Log("Show rooms");
            vsScreen.SetActive(false);
            NoOpponentScreen.SetActive(false);
            shopScreen.SetActive(false);

            mainScreen.SetActive(false);
            usernameScreen.SetActive(false);
            roomScreen.SetActive(true);
        }

        public void CheckCoinBalance(string message)
        {
            if (PhotonNetwork.IsConnected)
            {
                PhotonNetwork.Disconnect();
            }
            coinErrorPanel.SetActive(true);
            string msg = message;
            coinErrorText.text = msg;

            // Clear previous listeners before adding new one
            ErrorPanelHideBtn.onClick.RemoveAllListeners();
            ErrorPanelHideBtn.onClick.AddListener(HideCoinErrorPanel);
        }

        public void CheckConnection(string message)
        {
            if (PhotonNetwork.IsConnected)
            {
                PhotonNetwork.Disconnect();
            }
            coinErrorPanel.SetActive(true);
            string msg = message;
            coinErrorText.text = msg;

            // Clear previous listeners before adding new one
            ErrorPanelHideBtn.onClick.RemoveAllListeners();
            ErrorPanelHideBtn.onClick.AddListener(RefreshLoadScreen);
        }

        private void HideCoinErrorPanel()
        {
            coinErrorPanel.SetActive(false);
        }

        private void RefreshLoadScreen()
        {
            coinErrorPanel.SetActive(false);

    #if UNITY_WEBGL && !UNITY_EDITOR
            OpenUrlReplace("https://playport.lk");
    #else
            Application.OpenURL("https://playport.lk");
    #endif
        }

        private void setupScreens()
        {
            roomScreen.SetActive(false);
            NoOpponentScreen.SetActive(false);
            vsBackBtn.gameObject.SetActive(false);
            vsBackBtn.interactable = true;
            vsUsersParent.SetActive(false);
            vsScreen.SetActive(true);
        }

        public void RoomsBtn(string room)
        {
            string msg = "Your account balance is low!\r\nPlease top up your account\r\nto continue playing";
            int coinBal = UMdata.total_coins;

            if (coinBal <= 0)
            {
                CheckCoinBalance(msg);
            }
            else
            {
                PhotonController.Instance.whichRoom = room;
                if (room == "colombo" && Constants.ROOM_COLOMBO_ENTRY_PRICE <= coinBal)
                {
                    StartCoroutine(PostGameStatsCoroutine(room));
                    setupScreens();
                    PhotonController.Instance.roomEntryPice = Constants.ROOM_COLOMBO_ENTRY_PRICE;
                    PhotonController.Instance.GetBotStatus();
                    PlayerPrefs.SetString("roomName", room);
                    VsJoinedRoom();
                }
                else if (room == "galle" && Constants.ROOM_GALLE_ENTRY_PRICE <= coinBal)
                {
                    StartCoroutine(PostGameStatsCoroutine(room));
                    setupScreens();
                    PhotonController.Instance.roomEntryPice = Constants.ROOM_GALLE_ENTRY_PRICE;
                    PhotonController.Instance.GetBotStatus();
                    PlayerPrefs.SetString("roomName", room);
                    VsJoinedRoom();
                }
                else if (room == "kandy" && Constants.ROOM_KANDY_ENTRY_PRICE <= coinBal)
                {
                    StartCoroutine(PostGameStatsCoroutine(room));
                    setupScreens();
                    PhotonController.Instance.roomEntryPice = Constants.ROOM_KANDY_ENTRY_PRICE;
                    PhotonController.Instance.GetBotStatus();
                    PlayerPrefs.SetString("roomName", room);
                    VsJoinedRoom();
                }
                else if (room == "sigiri" && Constants.ROOM_SIGIRI_ENTRY_PRICE <= coinBal)
                {
                    StartCoroutine(PostGameStatsCoroutine(room));
                    setupScreens();
                    PhotonController.Instance.roomEntryPice = Constants.ROOM_SIGIRI_ENTRY_PRICE;
                    BR_APIManager.BotStateData botData = new BR_APIManager.BotStateData
                    {
                        is_bot_activated = false,
                        is_carrom_bot_activated = false,
                        current_carrom_sequence = null,
                        is_fresh_carrom_user = false,
                        is_fresh_user = false
                    };
                    PhotonController.Instance.FindRoom(botData);
                    PlayerPrefs.SetString("roomName", room);
                    VsJoinedRoom();
                }
                else if (room == "jaffna" && Constants.ROOM_JAFFNA_ENTRY_PRICE <= coinBal)
                {
                    StartCoroutine(PostGameStatsCoroutine(room));
                    setupScreens();
                    PhotonController.Instance.roomEntryPice = Constants.ROOM_JAFFNA_ENTRY_PRICE;

                    BR_APIManager.BotStateData botData = new BR_APIManager.BotStateData
                    {
                        is_bot_activated = false,
                        is_carrom_bot_activated = false,
                        current_carrom_sequence = null,
                        is_fresh_carrom_user = false,
                        is_fresh_user = false
                    };
                    PhotonController.Instance.FindRoom(botData);
                    PlayerPrefs.SetString("roomName", room);
                    VsJoinedRoom();
                }
                else
                {
                    CheckCoinBalance(msg);
                }
            }
        }

        public void VsBackBtn()
        {
            AudioManager.Instance.StopRollingSound();
            vsBackBtn.interactable = false;
            vsMsgText.text = "Leaveing room...";
            if (noOpponentFound != null)
            {
                StopCoroutine(noOpponentFound);
            }
            ResetBotSettings();
            PhotonNetwork.LeaveRoom();
        }

        public void HideOpponentPanel() {
            NoOpponentScreen.SetActive(false);

        }

        public void VsOnLeftRoom()
        {
            AudioManager.Instance.StopRollingSound();
            vsBackBtn.gameObject.SetActive(false);
            //vsBackBtn.interactable = true;
            vsUsersParent.SetActive(false);
            vsScreen.SetActive(false);
            NoOpponentScreen.SetActive(false);
            roomScreen.SetActive(true);
        }

        public void VsJoinedRoom()
        {
            vsTotalBetText.text = PhotonController.Instance.roomEntryPice.ToString("###,###,###") + "LKR";
            vsBackBtn.interactable = true;
            vsMsgText.text = "Waiting opponent...";
            vsUsersParent.SetActive(true);
            UpdateVsNames();
            OpponentStatus = 0;

            // Stop any existing coroutines first
            if (noOpponentFound != null)
            {
                StopCoroutine(noOpponentFound);
                noOpponentFound = null;
            }
            
            if (OpponentFound != null)
            {
                StopCoroutine(OpponentFound);
                OpponentFound = null;
            }

            int playerCount = PhotonNetwork.PlayerList.Length;
            Debug.Log($"VsJoinedRoom: Player count = {playerCount}");

            if (playerCount == 1)
            {
                // Only local player - start waiting for opponent
                noOpponentFound = NoOpponentCountdown();
                StartCoroutine(noOpponentFound);
                Debug.Log("Started NoOpponentCountdown - waiting for opponent");
            }
            else if (playerCount == 2)
            {
                // Opponent already in room
                OpponentFound = GowithOpponentCountdown();
                StartCoroutine(OpponentFound);
                Debug.Log("Started GowithOpponentCountdown - opponent found");
            }
            else
            {
                // Unexpected player count - treat as no opponent
                Debug.LogWarning($"Unexpected player count: {playerCount}");
                noOpponentFound = NoOpponentCountdown();
                StartCoroutine(noOpponentFound);
            }
        }

        IEnumerator GowithOpponentCountdown()
        {
            yield return new WaitForSeconds(3f);
            SetOS();
            Debug.Log("Opponent found");

            yield return new WaitForSeconds(3f);
            VsStartMatch();
        }

        IEnumerator NoOpponentCountdown()
        {
            yield return new WaitForSeconds(30f);
            AudioManager.Instance.StopRollingSound();
            Debug.Log("No opponent found");
            NoOpponentScreen.SetActive(true);
            if (PhotonNetwork.InRoom){
                PhotonNetwork.CurrentRoom.IsOpen = false;
                PhotonNetwork.CurrentRoom.IsVisible = false;
            }
        }

        public void WaitForOpponentBtn()
        {
            if (noOpponentFound == null)
            {
                noOpponentFound = KeepWaiting();
            }
            else
            {
                StopCoroutine(noOpponentFound);
                noOpponentFound = KeepWaiting();
            }

            StartCoroutine(noOpponentFound);
        }

        IEnumerator KeepWaiting()
        {
            PhotonNetwork.LeaveRoom();
            ResetBotSettings();

            waitingPanel.SetActive(true);
            yield return new WaitForSeconds(5f);

            waitingPanel.SetActive(false);

            string roomTmp = PlayerPrefs.GetString("roomName");
            RoomsBtn(roomTmp);
        }

        public void leaveBtn()
        {
            waitingPanel.SetActive(false);
            NoOpponentScreen.SetActive(false);
            AudioManager.Instance.StopRollingSound();
            Debug.Log("Leeaving RRoomMM4");
            if (noOpponentFound != null)
            {
                StopCoroutine(noOpponentFound);
            }

            if (PhotonNetwork.InRoom)
            {
                PhotonNetwork.LeaveRoom();
            }
            
            string token = GetTokenFromURL();
            if (!string.IsNullOrEmpty(token))
            {
                AUTH_TOKEN = token;
                Debug.Log("Token successfully retrieved and stored");
            }
            else
            {
                Debug.LogError("Failed to retrieve token from URL");
            }
            
            // ✅ NOW start loading screen (which will use the token)
            ShowLoadingScreen();
            mainScreen.SetActive(false);
            roomScreen.SetActive(false);
            vsScreen.SetActive(false);
            shopScreen.SetActive(false);
            usernameScreen.SetActive(false);

            ShowRooms();
        }

        public void VsOnPlayerJoinedRoom()
        {
            AudioManager.Instance.StopRollingSound();
            
            if (PhotonNetwork.CurrentRoom.PlayerCount == 2)
            {
                PhotonNetwork.CurrentRoom.IsOpen = false;
                PhotonNetwork.CurrentRoom.IsVisible = false;

                UpdateVsNames();
                
                // Stop the other coroutine
                if (OpponentFound != null)
                {
                    StopCoroutine(OpponentFound);
                }
                
                if (OpponentJoined == null)
                {
                    OpponentJoined = OpponentJoinedCountdown();
                }
                else
                {
                    StopCoroutine(OpponentJoined);
                    OpponentJoined = OpponentJoinedCountdown();
                }

                StartCoroutine(OpponentJoined);
            }
        }
        

        IEnumerator OpponentJoinedCountdown()
        {
            SetOS();

            yield return new WaitForSeconds(5f);
            VsStartMatch();
        }

        private void UpdateVsNames()
        {
            if (vsUsersParent && !vsUsersParent.activeSelf)
                vsUsersParent.SetActive(true);

            // Home (local player)
            string homeName = PhotonNetwork.LocalPlayer.NickName;
            if (vsHomeUsernameText) vsHomeUsernameText.text = LimitNameText(homeName);

            // Away (opponent)
            var others = PhotonNetwork.PlayerListOthers;
            if (others != null && others.Length > 0)
            {
                string awayName = others[0].NickName;
                if (vsAwayUsernameText) vsAwayUsernameText.text = LimitNameText(awayName);
                else vsAwayUsernameText.text = "";

                // Load opponent image
                if (others[0].CustomProperties.ContainsKey("profile_image_url"))
                {
                    string imageUrl = others[0].CustomProperties["profile_image_url"].ToString();
                    Debug.Log($"[MainMenu] Opponent profile image URL: {imageUrl}");
                    PlayerPrefs.SetString("opponentimage", imageUrl);
                    StartCoroutine(LoadOpponentImage(imageUrl));
                }
                else
                {
                    Debug.LogWarning("[MainMenu] Opponent has no profile image property set.");
                }
            }
        }

        public IEnumerator BotJoinedRoomCoroutine()
        {
            AudioManager.Instance.StopRollingSound();
            
            if (noOpponentFound != null)
            {
                StopCoroutine(noOpponentFound);
            }
            if (OpponentFound != null)
            {
                StopCoroutine(OpponentFound);
            }
            if (OpponentJoined != null)
            {
                StopCoroutine(OpponentJoined);
            }

            if (PhotonController.Instance.demoPlayer != null)
            {
                // yield return new WaitForSeconds(UnityEngine.Random.Range(1f, 3f));
                // yield return new WaitForSeconds(0f);

                vsAwayUsernameText.text = PhotonController.Instance.demoPlayer.name;
                OpponentImg.sprite = PhotonController.Instance.demoPlayer.profilePic;

                OpponentStatus = 1;

                yield return new WaitForSeconds(UnityEngine.Random.Range(3f, 6f));

                VsStartMatch();
            }
            else
            {
                WebBrowserBridge.Instance.ReloadCurrentPage();
            } 
        }
        
        void VsStartMatch()
        {
            if (!PhotonController.Instance.offlineMatch)
            {
                if (PhotonNetwork.PlayerList.Length == 2)
                {
                    if (PhotonNetwork.IsMasterClient)
                    {
                        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable();
                        props["BetCount"] = 0;
                        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
                    }

                    if (noOpponentFound != null)
                    {
                        StopCoroutine(noOpponentFound);
                    }
                    if (OpponentFound != null)
                    {
                        StopCoroutine(OpponentFound);
                    }
                    if (OpponentJoined != null)
                    {
                        StopCoroutine(OpponentJoined);
                    }
                }
            }
            else
            {
                if (noOpponentFound != null)
                {
                    StopCoroutine(noOpponentFound);
                }
                if (OpponentFound != null)
                {
                    StopCoroutine(OpponentFound);
                }
                if (OpponentJoined != null)
                {
                    StopCoroutine(OpponentJoined);
                }
            }
            
            int currentCoin = tempCoin;
            int newCoin = currentCoin - PhotonController.Instance.roomEntryPice;

            vsBackBtn.gameObject.SetActive(false);

            LeanTween.value(PhotonController.Instance.roomEntryPice, 0, 1f).setOnUpdate((float val) =>
            {
                vsHomeUserBetText.text = "" + (int)val;
                vsAwayUserBetText.text = "" + (int)val;
            });

            LeanTween.value(currentCoin, newCoin, 1f).setOnUpdate((float val) =>
            {
                if (val < 1)
                {
                    coinText.text = "0";
                }
                else
                {
                    coinText.text = val.ToString();
                }
            });

            if (!PhotonController.Instance.offlineMatch)
            {    
                if (PhotonNetwork.IsMasterClient)
                {
                    PhotonNetwork.LoadLevel("Game");
                }
            }
            else
            {
                SceneManager.LoadScene("Game");
            }
        }

        private int _opponentStatus;

        public int OpponentStatus
        {
            get { return _opponentStatus; }
            set
            {
                _opponentStatus = value;

                if (_opponentStatus == 0)
                {
                    vsAwayUsernameText.gameObject.SetActive(false);
                    AudioManager.Instance.PlayRollingSound();
                    dp.Play("0");
                    coinflow.Play("0");
                    coinflow1.Play("0");
                    numlaod.Play("0");
                    nameload.Play("0");
                    coinBonus.Play("0");
                    OpponentImg.color = new Color(1f, 1f, 1f, 0f);
                }
                else if (_opponentStatus == 1)
                {
                    vsAwayUsernameText.gameObject.SetActive(true);
                    AudioManager.Instance.StopRollingSound();
                    AudioManager.Instance.PlayCoinCollectSound();
                    dp.Play("1");
                    coinflow.Play("1");
                    coinflow1.Play("1");
                    numlaod.Play("1");
                    nameload.Play("1");
                    coinBonus.Play("1");
                    OpponentImg.color = new Color(1f, 1f, 1f, 1f);
                    LeanTween.value(PhotonController.Instance.roomEntryPice, PhotonController.Instance.roomEntryPice * 2, 1f).setOnUpdate((float val) =>
                    {
                        vsTotalBetText.text = "" + (int)val;
                    }).setOnComplete(() =>
                    {
                        UpdateCurrencyText();
                    });
                }
                else if (_opponentStatus == 2)
                {
                    dp.Play("1");
                    coinflow.Play("0");
                    coinflow1.Play("0");
                    numlaod.Play("0");
                    nameload.Play("1");
                    coinBonus.Play("1");
                }
                else if (_opponentStatus == 3)
                {
                    dp.Play("1");
                    coinflow.Play("0");
                    coinflow1.Play("0");
                    numlaod.Play("0");
                    nameload.Play("1");
                    coinBonus.Play("1");
                }
            }
        }

        public void endNumberload()
        {
            OpponentS = 2;
            OpponentStatus = 2;
        }

        public void SetOS()
        {
            OpponentStatus = 1;
        }

        public Sprite GetRandomDemoProfile()
        {
            List<Sprite> demoProfiles;

            if (DateTime.Now.Hour < 12)
            {
                demoProfiles = demoProfiles1;
            }
            else
            {
                demoProfiles = demoProfiles1;
            }

            if (demoProfiles != null && demoProfiles.Count > 0)
            {
                return demoProfiles[UnityEngine.Random.Range(0, demoProfiles.Count)];
            }
            return null;
        }
    }
}