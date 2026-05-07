using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using BEKStudio;
using Newtonsoft.Json;

public class BR_APIManager : MonoBehaviour
{
    private static BR_APIManager instance;

    public static BR_APIManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("BR_APIManager");
                instance = go.AddComponent<BR_APIManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeGameID();
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
    public class UpdateGameStatsRequest
    {
        public string game_id;
        public int wins;
        public int losses;
    }

    [System.Serializable]
    public class GameStatsResponse
    {
        public bool success;
        public string message;
    }

    [System.Serializable]
    public class UpdateProfileRequest
    {
        public int total_coins;
    }

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
        public int total_coins;
        public string profile_image_url;
    }

    [System.Serializable]
    public class BetTransactionRequest
    {
        public string game_id;
        public int bet_amount;
        public string result;
        public string round_id;
        public string match_data;
    }

    [System.Serializable]
    public class BotBetTransactionRequest
    {
        public string game_id;
        public int bet_amount;
        public string result;
        public string round_id;
        public string match_data;
        public bool current_sequence = true;
    }

    [System.Serializable]
    public class BetTransactionResponse
    {
        public bool success;
        public string message;
    }

    [System.Serializable]
    public class FetchGameDataResponse
    {
        public bool success;
        public string message;
        public GameData data;
    }

    [System.Serializable]
    public class GameData
    {
        public string version;
    }

    [System.Serializable]
    public class BotStateResponse
    {
        public bool success;
        public BotStateData data;
    }

    [System.Serializable]
    public class BotStateData
    {
        public bool is_fresh_user;
        public bool is_fresh_carrom_user;
        public bool is_bot_activated;
        public bool is_carrom_bot_activated;
        public bool? current_carrom_sequence;

    }

    [System.Serializable]
    public class BotMatchDetailsRequest
    {
        public string gameId;
    }

    [System.Serializable]
    public class BotMatchData
    {
        public bool is_fresh_user;
        public bool is_bot_activated;
        public int MatchCompleted;
        public int allMatchCompleted;
        public bool? current_sequence;
    }

    [System.Serializable]
    public class BotMatchStateResponse
    {
        public bool success;
        public string message;
        public BotMatchData data;
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

    public void UpdateWins()
    {
        StartCoroutine(UpdateGameStatsCoroutine(wins: 1, losses: 0));
    }

    public void UpdateLosses()
    {
        StartCoroutine(UpdateGameStatsCoroutine(wins: 0, losses: 1));
    }

    public IEnumerator UpdateGameStatsCoroutine(int wins, int losses)
    {
        if (string.IsNullOrEmpty(GAME_ID))
        {
            Debug.LogError("[UpdateGameStats] GAME_ID is empty!");
            yield break;
        }

        if (string.IsNullOrEmpty(AUTH_TOKEN))
        {
            Debug.LogError("[UpdateGameStats] AUTH_TOKEN is empty!");
            yield break;
        }

        Debug.Log($"[UpdateGameStats] Sending update with GAME_ID: {GAME_ID}");

        string json;
        if (wins > 0)
        {
            json = $"{{\"game_id\":\"{GAME_ID}\",\"wins\":{wins}}}";
        }
        else
        {
            json = $"{{\"game_id\":\"{GAME_ID}\",\"losses\":{losses}}}";
        }

        using (var request = new UnityWebRequest("https://api.playport.lk/api/v1/games-stats", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", "Bearer " + AUTH_TOKEN);
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            // Check for 401 first
            if (CheckForUnauthorized(request))
            {
                yield break;
            }

            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"[UpdateGameStats] Error: {request.error} (Code: {request.responseCode})");
                yield break;
            }

            string jsonResponse = request.downloadHandler.text;
            Debug.Log("[UpdateGameStats] Response: " + jsonResponse);

            GameStatsResponse response = JsonUtility.FromJson<GameStatsResponse>(jsonResponse);
            if (response != null && response.success)
            {
                Debug.Log($"[UpdateGameStats] Success! {(wins > 0 ? "Win" : "Loss")} recorded.");
            }
        }
    }

    public IEnumerator UpdateUserProfileCoroutine_PUT(int newTotalCoins)
    {
        if (string.IsNullOrEmpty(AUTH_TOKEN))
        {
            Debug.LogError("[UpdateUserProfile] AUTH_TOKEN is empty!");
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

            // Check for 401 first
            if (CheckForUnauthorized(request))
            {
                yield break;
            }

            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"[UpdateUserProfile] Error: {request.error} (Code: {request.responseCode})");
                yield break;
            }

            string jsonResponse = request.downloadHandler.text;
            Debug.Log("[UpdateUserProfile] Response: " + jsonResponse);

            ApiResponse response = JsonUtility.FromJson<ApiResponse>(jsonResponse);
            if (response != null && response.success)
            {
                Debug.Log("[UpdateUserProfile] Success! Coins updated." + newTotalCoins);
            }
        }
    }

    public IEnumerator FetchGameData(System.Action<string> callback)
    {
        if (string.IsNullOrEmpty(GAME_ID))
        {
            Debug.LogError("[FetchGameData] GAME_ID is empty!");
            callback?.Invoke(null);
            yield break;
        }

        if (string.IsNullOrEmpty(AUTH_TOKEN))
        {
            Debug.LogError("[FetchGameData] AUTH_TOKEN is empty!");
            callback?.Invoke(null);
            yield break;
        }

        string url = $"https://api.playport.lk/api/v1/games/{GAME_ID}";

        Debug.Log($"[FetchGameData] GET {url}");

        using (var request = UnityWebRequest.Get(url))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", "Bearer " + AUTH_TOKEN);
            request.SetRequestHeader("Accept", "application/json");

            yield return request.SendWebRequest();

            long responseCode = request.responseCode;
            string responseText = null;

            try
            {
                responseText = request.downloadHandler != null
                    ? request.downloadHandler.text
                    : null;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[FetchGameData] Could not read response text: " + ex.Message);
            }

            Debug.Log($"[FetchGameData] Response code: {responseCode}");
            Debug.Log($"[FetchGameData] Response body: {responseText}");

            // Handle 401 centrally (same pattern as your bet API)
            if (CheckForUnauthorized(request))
            {
                callback?.Invoke(null);
                yield break;
            }

            // Network / HTTP error
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[FetchGameData] Network/HTTP Error: {request.error} (Code: {responseCode})");
                callback?.Invoke(null);
                yield break;
            }

            if (string.IsNullOrEmpty(responseText))
            {
                Debug.LogWarning("[FetchGameData] Empty response body");
                callback?.Invoke(null);
                yield break;
            }

            FetchGameDataResponse response = null;

            try
            {
                response = JsonUtility.FromJson<FetchGameDataResponse>(responseText);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[FetchGameData] JSON parse error: " + ex.Message);
                callback?.Invoke(null);
                yield break;
            }

            if (response == null || !response.success || response.data == null)
            {
                Debug.LogWarning("[FetchGameData] API returned invalid data");
                callback?.Invoke(null);
                yield break;
            }

            Debug.Log("[FetchGameData] SUCCESS - version: " + response.data.version);
            callback?.Invoke(response.data.version);
        }
    }

    public void PostBetTransaction(int betAmount, string result, string roundId, System.Action<bool, string> callback, string matchData="")
    {
        StartCoroutine(PostBetTransactionCoroutine(betAmount, result, roundId, callback, matchData));
    }

    public IEnumerator PostBetTransactionCoroutine(int betAmount, string result, string roundId, System.Action<bool, string> callback, string matchData)
    {
        if (string.IsNullOrEmpty(GAME_ID))
        {
            Debug.LogError("[PostBetTransaction] GAME_ID is empty!");
            callback?.Invoke(false, "GAME_ID empty");
            yield break;
        }

        if (string.IsNullOrEmpty(AUTH_TOKEN))
        {
            Debug.LogError("[PostBetTransaction] AUTH_TOKEN is empty!");
            callback?.Invoke(false, "AUTH_TOKEN empty");
            yield break;
        }

        string botName = PhotonController.Instance.demoPlayer != null ? PhotonController.Instance.demoPlayer.name : "";

        string json;

        if (PhotonController.Instance.offlineMatch)
        {
            var betRequest = new BotBetTransactionRequest
            {
                game_id = GAME_ID,
                bet_amount = betAmount,
                result = result,
                round_id = roundId,
                match_data = "[DEMO] " + botName,
                current_sequence = PhotonController.Instance.demoPlayer.isFresh ? !PhotonController.Instance.demoPlayer.isWinning : true
            };

            json = JsonUtility.ToJson(betRequest);
        }
        else
        {
            
            var betRequest = new BetTransactionRequest
            {
                game_id = GAME_ID,
                bet_amount = betAmount,
                result = result,
                round_id = roundId,
                match_data = ""
            };

            json = JsonUtility.ToJson(betRequest);
        }

        // Log the outgoing payload for debugging
        Debug.Log($"[PostBetTransaction] POST https://api.playport.lk/api/v1/transactions/bet");
        Debug.Log($"[PostBetTransaction] Request JSON: {json}");
        Debug.Log($"[PostBetTransaction] AUTH_TOKEN present: {(!string.IsNullOrEmpty(AUTH_TOKEN)).ToString()}");

        using (var request = new UnityWebRequest("https://api.playport.lk/api/v1/transactions/bet", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Authorization", "Bearer " + AUTH_TOKEN);
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");

            yield return request.SendWebRequest();

            long responseCode = request.responseCode;
            string responseText = null;
            try
            {
                responseText = request.downloadHandler != null ? request.downloadHandler.text : null;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[PostBetTransaction] Could not read downloadHandler.text: " + ex.Message);
            }

            Debug.Log($"[PostBetTransaction] Response code: {responseCode}");
            Debug.Log($"[PostBetTransaction] Response body: {responseText}");

            // Handle 401 centrally
            if (CheckForUnauthorized(request))
            {
                callback?.Invoke(false, "401 Unauthorized");
                yield break;
            }

            // For non-successful HTTP codes (including 500), log the details and return the server message if present
            if (request.result != UnityWebRequest.Result.Success)
            {
                string err = request.error;
                string serverMsg = !string.IsNullOrEmpty(responseText) ? responseText : err;
                Debug.LogError($"[PostBetTransaction] Network/HTTP Error: {err} (Code: {responseCode}) - Server message: {serverMsg}");
                callback?.Invoke(false, serverMsg);
                yield break;
            }

            // Success path (200-299)
            if (!string.IsNullOrEmpty(responseText))
            {
                BetTransactionResponse response = null;
                try
                {
                    response = JsonUtility.FromJson<BetTransactionResponse>(responseText);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("[PostBetTransaction] JSON parse error: " + ex.Message);
                }

                if (response != null)
                {
                    if (response.success)
                    {
                        Debug.Log("[PostBetTransaction] SUCCESS: " + response.message);
                        callback?.Invoke(true, response.message ?? "Success");
                    }
                    else
                    {
                        Debug.LogWarning("[PostBetTransaction] API returned success=false: " + response.message);
                        callback?.Invoke(false, response.message ?? "API returned success=false");
                    }
                }
                else
                {
                    Debug.LogWarning("[PostBetTransaction] Empty/invalid response JSON, returning raw response text");
                    callback?.Invoke(true, responseText);
                }
            }
            else
            {
                Debug.LogWarning("[PostBetTransaction] Empty response body but request succeeded.");
                callback?.Invoke(true, "Success (empty response)");
            }
        }
    }

    public void GetBotState()
    {
        StartCoroutine(GetBotStateCoroutine(GetTokenFromURL()));
    }

    private IEnumerator GetBotStateCoroutine(string token)
    {
        string gameId = "3a722139-cb47-46de-a8f6-2e2d041361f5";

        string url = $"https://api.playport.lk/api/v1/bot-match/state?gameId=3a722139-cb47-46de-a8f6-2e2d041361f5";

        Debug.Log("ABCD: " + url);

        UnityWebRequest request = UnityWebRequest.Get(url);

        // Headers
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + token);

        yield return request.SendWebRequest();

        string json = request.downloadHandler.text;

        if (request.result == UnityWebRequest.Result.Success)
        {
            BotStateResponse response = JsonConvert.DeserializeObject<BotStateResponse>(json);
            Debug.Log("Bot State Success. Response: " + json);

            PhotonController.Instance.FindRoom(
                response.data
            );
        }
        else
        {
            Debug.LogError("Bot State Failed: " + request.error);
            Debug.LogError("Message: " + json);
            Debug.LogError("Status Code: " + request.responseCode);
            BR_APIManager.BotStateData botData = new BR_APIManager.BotStateData
            {
                is_bot_activated = false,
                is_carrom_bot_activated = false,
                current_carrom_sequence = null,
                is_fresh_carrom_user = false,
                is_fresh_user = false
            };
            PhotonController.Instance.FindRoom(botData);
        }
    }

    public void UpdateBotCount()
    {
        StartCoroutine(UpdateBotCountCoroutine());
    }

    private IEnumerator UpdateBotCountCoroutine()
    {
        var requestBody = new BotMatchDetailsRequest
        {
            gameId = GAME_ID
        };

        string json = JsonUtility.ToJson(requestBody);
        Debug.Log("[PostBotMatchState] Sending: " + json);


        using (var request = new UnityWebRequest("https://api.playport.lk/api/v1/bot-match/complete", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Authorization", "Bearer " + AUTH_TOKEN);
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"[PostBotMatchState] Error: {request.error} (HTTP {request.responseCode})");
                Debug.LogError($"[PostBotMatchState] Response: {request.downloadHandler.text}");
                // callback?.Invoke(false, null);
                yield break;
            }

            string jsonResponse = request.downloadHandler.text;
            Debug.Log("[PostBotMatchState] Raw Response: " + jsonResponse);

            BotMatchStateResponse response = null;
            try
            {
                response = JsonUtility.FromJson<BotMatchStateResponse>(jsonResponse);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[PostBotMatchState] JSON parse error: " + ex.Message);
                // callback?.Invoke(false, null);
                yield break;
            }

            if (response != null && response.success)
            {
                Debug.Log("[PostBotMatchState] ✅ Success!");
                // callback?.Invoke(true, botMatchData);
            }
            else
            {
                string msg = response?.message ?? "Unknown error";
                Debug.LogWarning("[PostBotMatchState] API returned success=false: " + msg);
                // callback?.Invoke(false, null);
            }
        }
    }

    private void InitializeGameID()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        string url = Application.absoluteURL;
        Debug.Log("[InitializeGameID] Full URL: " + url);
        
        if (!string.IsNullOrEmpty(url))
        {
            try
            {
                int queryStart = url.IndexOf('?');
                if (queryStart >= 0)
                {
                    string queryString = url.Substring(queryStart + 1);
                    Debug.Log("[InitializeGameID] Query string: " + queryString);
                    
                    string[] parameters = queryString.Split('&');
                    
                    foreach (string param in parameters)
                    {
                        Debug.Log("[InitializeGameID] Processing parameter: " + param);
                        
                        int equalIndex = param.IndexOf('=');
                        if (equalIndex > 0)
                        {
                            string key = param.Substring(0, equalIndex);
                            string value = param.Substring(equalIndex + 1);
                            
                            // Decode the URL-encoded value
                            value = UnityEngine.Networking.UnityWebRequest.UnEscapeURL(value);
                            
                            if (key == "gameid" || key == "gameId" || key == "game_id")
                            {
                                Debug.Log("[InitializeGameID] Found gameid parameter with value length: " + value.Length);
                                
                                // Validate that the value is long enough to contain both game ID and token
                                if (value.Length > 36)
                                {
                                    try
                                    {
                                        // Extract Game ID (first 36 characters - UUID format)
                                        GAME_ID = value.Substring(0, 36);
                                        Debug.Log("[InitializeGameID] Extracted Game ID: " + GAME_ID);
                                        
                                        // Extract Token (everything after the first 36 characters)
                                        AUTH_TOKEN = value.Substring(36);
                                        Debug.Log("[InitializeGameID] Extracted Token (first 20 chars): " + AUTH_TOKEN.Substring(0, Mathf.Min(20, AUTH_TOKEN.Length)) + "...");
                                    }
                                    catch (System.Exception ex)
                                    {
                                        Debug.LogError("[InitializeGameID] Error extracting Game ID and Token: " + ex.Message);
                                    }
                                }
                                else
                                {
                                    Debug.LogError("[InitializeGameID] gameid parameter value is too short. Expected > 36 characters, got: " + value.Length);
                                }
                                break;
                            }
                        }
                    }
                }
                
                if (string.IsNullOrEmpty(GAME_ID))
                {
                    Debug.LogWarning("[InitializeGameID] No GAME_ID extracted from URL");
                }
                
                if (string.IsNullOrEmpty(AUTH_TOKEN))
                {
                    Debug.LogWarning("[InitializeGameID] No AUTH_TOKEN extracted from URL");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[InitializeGameID] Error parsing URL: " + e.Message);
            }
        }
        else
        {
            Debug.LogWarning("[InitializeGameID] Application.absoluteURL is empty");
        }
#else
        // For testing in editor
        GAME_ID = "3a722139-cb47-46de-a8f6-2e2d041361f5";
        AUTH_TOKEN = MenuController.Instance.user_token;
        Debug.Log($"[InitializeGameID] Using test GAME_ID in editor: {GAME_ID}");
        Debug.Log($"[InitializeGameID] Using test AUTH_TOKEN in editor");
#endif
    }

    public string GetTokenFromURL()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        string url = Application.absoluteURL;
        Debug.Log("[GetTokenFromURL] Full URL: " + url);
        
        if (!string.IsNullOrEmpty(url))
        {
            try
            {
                int queryStart = url.IndexOf('?');
                if (queryStart >= 0)
                {
                    string queryString = url.Substring(queryStart + 1);
                    Debug.Log("[GetTokenFromURL] Query string: " + queryString);
                    
                    string[] parameters = queryString.Split('&');
                    string token = null;
                    
                    foreach (string param in parameters)
                    {
                        Debug.Log("[GetTokenFromURL] Processing parameter: " + param);
                        
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
                                Debug.Log("[GetTokenFromURL] Found gameid parameter with value length: " + value.Length);
                                
                                // Validate that the value is long enough to contain both game ID and token
                                if (value.Length > 36)
                                {
                                    try
                                    {
                                        // Extract Game ID (first 36 characters - UUID format)
                                        GAME_ID = value.Substring(0, 36);
                                        Debug.Log("[GetTokenFromURL] Extracted Game ID: " + GAME_ID);
                                        
                                        // Extract Token (everything after the first 36 characters)
                                        token = value.Substring(36);
                                        Debug.Log("[GetTokenFromURL] Extracted Token (first 20 chars): " + token.Substring(0, Mathf.Min(20, token.Length)) + "...");
                                    }
                                    catch (System.Exception ex)
                                    {
                                        Debug.LogError("[GetTokenFromURL] Error extracting Game ID and Token: " + ex.Message);
                                    }
                                }
                                else
                                {
                                    Debug.LogError("[GetTokenFromURL] gameid parameter value is too short. Expected > 36 characters, got: " + value.Length);
                                }
                                break;
                            }
                        }
                    }
                    
                    if (token != null && !string.IsNullOrEmpty(GAME_ID))
                    {
                        Debug.Log("[GetTokenFromURL] Successfully extracted both Game ID and Token");
                        return token;
                    }
                    else if (string.IsNullOrEmpty(GAME_ID))
                    {
                        Debug.LogWarning("[GetTokenFromURL] Game ID not extracted from URL");
                    }
                    else
                    {
                        Debug.LogWarning("[GetTokenFromURL] Token not extracted from URL");
                    }
                }
                else
                {
                    Debug.LogWarning("[GetTokenFromURL] No query string found in URL");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[GetTokenFromURL] Error parsing URL: " + e.Message);
            }
        }
        else
        {
            Debug.LogWarning("[GetTokenFromURL] Application.absoluteURL is null or empty");
        }
#else
        // Editor mode - return test token
        Debug.Log("[GetTokenFromURL] Not running in WebGL build - using test token");
        GAME_ID = "3a722139-cb47-46de-a8f6-2e2d041361f5";
        return MenuController.Instance.user_token;
#endif

        Debug.LogError("[GetTokenFromURL] Failed to extract token and game ID from URL");
        return null;
    }
}