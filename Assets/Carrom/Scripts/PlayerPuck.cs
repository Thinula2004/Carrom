using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Photon.Pun;
using Unity.VisualScripting;

namespace BEKStudio
{

    public class Shot
    {
        public List<GameObject> puckQueue; // cannon: [blocker, target] — normal: [target]
        public float angle;
        public Vector2 discPosition;
        public Vector2 holePosition;
        public string type = "";

        public GameObject firstPuck => puckQueue != null && puckQueue.Count > 1
        ? puckQueue[0]
        : null;

        public GameObject targetPuck => puckQueue != null && puckQueue.Count > 0
        ? puckQueue[puckQueue.Count - 1]
        : null;

        public void PrintData()
        {
            if (puckQueue != null)
            {
                Debug.Log("C: Puck Queue");
                foreach (GameObject puck in puckQueue)
                {
                    Debug.Log($"C: {puck.name}");
                }
                Debug.Log($"C: Shot \nDiscPos ({discPosition.x}, {discPosition.y})\nAngle {angle}\nHolePos {holePosition}");
            }
        }
    }

    public class HoleLayout
    {
        public Transform[] All;
        public Transform TopLeft, BottomLeft, TopRight, BottomRight;
        public float MidX;
        public float Line12Left, Line12Right;
    }

    public struct ShotLines
    {
        public Vector2 L1LS, L1LE, L1RS, L1RE;
        public Vector2 L2LS, L2LE, L2RS, L2RE;

        public static ShotLines Make(
            Vector2 l1ls, Vector2 l1le, Vector2 l1rs, Vector2 l1re,
            Vector2 l2ls, Vector2 l2le, Vector2 l2rs, Vector2 l2re)
            => new ShotLines
            {
                L1LS = l1ls,
                L1LE = l1le,
                L1RS = l1rs,
                L1RE = l1re,
                L2LS = l2ls,
                L2LE = l2le,
                L2RS = l2rs,
                L2RE = l2re
            };
    }

    /// <summary>A single direct (non-cannon) shot candidate.</summary>
    public class ShotCandidate
    {
        public bool Valid;
        public float Score = float.MaxValue;
        public Vector2 StrikerPos, ShootDir, PuckPos, HolePos;
        public string HoleName = "";
        public ShotLines Lines;
        public bool IsNearZeroUp, IsNearZeroDown, IsNearZeroTopOpposite;

        public bool TryUpdate(float score, Vector2 strikerPos, Vector2 shootDir,
                            Vector2 puckPos, Vector2 holePos, string holeName, ShotLines lines)
        {
            if (score >= Score) return false;
            Score = score; Valid = true;
            StrikerPos = strikerPos; ShootDir = shootDir;
            PuckPos = puckPos; HolePos = holePos;
            HoleName = holeName; Lines = lines;
            return true;
        }
    }

    /// <summary>A cannon shot: striker → blocker → target → hole.</summary>
    public class CannonCandidate
    {
        public bool Valid;
        public float Score = float.MaxValue;
        public Vector2 StrikerPos, ShootDir, BlockerPos, TargetPos, HolePos;
        public string HoleName = "";
        public float BlockerR;
        public GameObject BlockerGO, TargetGO;
        public ShotLines Lines;

        public bool TryUpdate(float score, Vector2 strikerPos, Vector2 shootDir,
                            Vector2 blockerPos, float blockerR,
                            Vector2 targetPos, Vector2 holePos, string holeName,
                            GameObject blockerGO, GameObject targetGO, ShotLines lines)
        {
            if (score >= Score) return false;
            Score = score; Valid = true;
            StrikerPos = strikerPos; ShootDir = shootDir;
            BlockerPos = blockerPos; BlockerR = blockerR;
            TargetPos = targetPos; HolePos = holePos; HoleName = holeName;
            BlockerGO = blockerGO; TargetGO = targetGO;
            Lines = lines;
            return true;
        }
    }

    /// <summary>All best-shot trackers accumulated during the black-puck scan.</summary>
    public class BlackScanState
    {
        public ShotCandidate Normal = new ShotCandidate();
        public ShotCandidate NearZeroTopOpposite = new ShotCandidate();
        public ShotCandidate NearZeroBottomSame = new ShotCandidate();
        public ShotCandidate HighPuckForced = new ShotCandidate();
        public ShotCandidate Fallback = new ShotCandidate();
        public ShotCandidate BankShot = new ShotCandidate();
        public CannonCandidate BankCannon = new CannonCandidate();
        public CannonCandidate Cannon = new CannonCandidate();

        public bool HasAnyNearZero;
        public float NearZeroA2Min = float.MaxValue, NearZeroA2Max = float.MinValue;
        public bool HasValidBottomShot;
        public bool FoundUsableTopFallbackCandidate;

        // Angle debug data — updated whenever a new best shot is stored
        public float A1Up, A1Down, A2, A3, Scale;
        public bool IsNearZeroUp, IsNearZeroDown;
        public string FallbackBlockerName = "";
    }



    /// <summary>All best-shot trackers accumulated during the red-puck scan.</summary>
    public class RedScanState
    {
        public ShotCandidate DirectShot = new ShotCandidate();
        public ShotCandidate Fallback = new ShotCandidate();
        public ShotCandidate BankShot = new ShotCandidate();
        public CannonCandidate Cannon = new CannonCandidate();

        // Near-zero flags for the best direct shot found
        public bool IsNearZeroUp, IsNearZeroDown, IsNearZeroTopOpposite;
    }

    /// <summary>Resolved shot info passed from SelectFinalShot → BuildFinalShot.</summary>
    public class FinalShotInfo
    {
        public string Type;
        public Vector2 StrikerPos, ShootDir, PuckPos, HolePos;
        public string HoleName;
        public ShotLines Lines;

        // Classification flags
        public bool IsRedTarget => Type.StartsWith("RED PUCK SHOT");
        public bool IsCannonShot => Type == "CANNON SHOT";
        public bool IsRedCannon => Type == "RED CANNON SHOT" || Type == "RED CANNON SHOT (MANDATORY)";

        // Near-zero flags forwarded from the winning candidate
        public bool IsNearZeroUp, IsNearZeroDown, IsNearZeroTopOpposite;

        // Cannon-specific extras
        public Vector2 CannonTargetPos, CannonBlockerPos;
        public float CannonBlockerR;
        public GameObject CannonBlockerGO, CannonTargetGO;
    }

    public class PlayerPuck : MonoBehaviourPun, IPunObservable
    {
        public static PlayerPuck Instance;
        [HideInInspector] public Rigidbody2D rb;
        SpriteRenderer spriteRenderer;
        public bool isReadyShoot = true;
        PhotonView photonView;
        public PhysicsMaterial2D physicMaterial;
        public CircleCollider2D collider;
        public Vector2 startPos;
        public Vector2 endPos;
        public bool isTouch;
        float opponentSliderValue;
        Vector2 puckStartPos;
        public GameObject arrow;
        public float force;
        public float forceMultiplier;
        Vector2 lastVel;
        public bool isMoving;
        private Vector2 arrowLocalScale;
        public GameObject tutorial;
        public GameObject strikPuck;

        public float tutorialShowDelay = 2;
        public float strikPuckShowDelay = 2;

        private bool tutorialShownBefore = false;

        public int chanceCount;

        public AudioClip DiskPotClip;
        [SerializeField] private AudioSource AudioSource;

        [Header("Velocity Settings")]
        [SerializeField] private float minVelocity = 0.5f;
        [SerializeField] private float maxVelocity = 10f;
        [SerializeField] private float minVolume = 0.3f;
        [SerializeField] private float maxVolume = 1f;

        [Header("Bot Settings")]
        [Range(0f, 1f)]
        [SerializeField] private float bottomCornerShortAngleScale = 0.2f; // 0 = no angle, 1 = full angle
        [SerializeField] private float bottomCornerShortDist1Threshold = 0.6f; // max dist1 to trigger
        [SerializeField] private float bottomCornerNearA1Threshold = 70f;  // min angle1Up to trigger

        private bool touchEnabled = true;
        public Shot selectedShot { get; set; }
        public GameObject clone;
        private List<LineRenderer> botLines = new List<LineRenderer>();

        [SerializeField] private float highPuckTopHoleAngleScale = 0.5f; // tune this to fix the angle
        bool correctPuckHit = false;
        bool correctWallHit = false;

        private bool repelModeActive = false;
        private Coroutine repelCoroutine;

        [Header("Repel Mode")]
        [SerializeField] private float repelStartDistance = 2f;   // distance at which damping starts increasing
        [SerializeField] private float repelMaxDamping = 100f;    // damping applied when very close to a hole
        [SerializeField] private float repelBaseDamping = 2f;     // damping restored when repel is off

        [Header("Bank Shot Settings")]
        [Range(0f, 1f)]
        [SerializeField] private float bankAngleBias = .9f;

        List<Shot> breakShots = new List<Shot>
        {
            new Shot
            {
                puckQueue = null,
                angle = 114f + 180f,
                discPosition = new Vector2(-1.223f, 1.649f),
                holePosition = Vector2.zero
            },
            new Shot
            {
                puckQueue = null,
                angle = 97.835f + 180f,
                discPosition = new Vector2(-0.340f, 1.649f),
                holePosition = Vector2.zero
            },
            new Shot
            {
                puckQueue = null,
                angle = 93f + 180f,
                discPosition = new Vector2(0.0887f, 1.649f),
                holePosition = Vector2.zero
            },
        };

        public bool isShotDone;

        private struct WallInfo
        {
            public bool IsVertical;   // true = left/right wall, false = bottom wall
            public float Position;     // x for vertical, y for horizontal
            public float Min, Max;     // bounds along the other axis
        }

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

        void Start()
        {
            isShotDone = false;
            correctPuckHit = false;
            correctWallHit = false;
            clone.SetActive(false);
            chanceCount = 0;
            rb = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            photonView = GetComponent<PhotonView>();

            puckStartPos = transform.localPosition;

            opponentSliderValue = 0.5f;

            lastVel = Vector2.zero;
            isMoving = false;
            arrowLocalScale = arrow.transform.localScale;

            // reduce tunnelling on player puck
            try
            {
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            }
            catch { }
        }

        public void EnableTouchControls()
        {
            touchEnabled = true;
        }

        public void DisableTouchControls()
        {
            touchEnabled = false;

            // Safety reset (important)
            isTouch = false;
            arrow.SetActive(false);
            forceMultiplier = 0;
        }

        public void DisableCollider()
        {
            Debug.Log("D: Disabling collider");
            collider.isTrigger = true;
        }

        public void EnableCollider()
        {
            Debug.Log("D: Enabling collider");
            collider.isTrigger = false;
        }

        // Updated to match Puck.cs sound system
        private float GetVolumeFromVelocity(float velocity)
        {
            float normalized = Mathf.InverseLerp(minVelocity, maxVelocity, velocity);
            float volume = Mathf.Lerp(minVolume, maxVolume, normalized);
            return Mathf.Clamp01(volume);
        }

        public void PlayPocketSound(AudioClip clip, float velocity, Vector3 pos)
        {
            if (clip == null) return;
            float volume = GetVolumeFromVelocity(velocity);
            float fixedPitch = 1f;

            if (SFXManager.Instance != null)
            {
                SFXManager.Instance.PlayAtPosition(clip, pos, volume, fixedPitch, 0f);
                return;
            }

            if (AudioSource != null)
            {
                AudioSource.PlayOneShot(clip, volume);
            }
        }
        private void CheckForOverlaps()
        {
            // Check if we're actually overlapping with any coins
            bool actuallyOverlapping = false;

            Collider2D[] overlaps = Physics2D.OverlapCircleAll(transform.position, collider.bounds.extents.x);

            foreach (Collider2D col in overlaps)
            {
                if (col.gameObject == gameObject) continue; // Skip self

                if (col.gameObject.tag == "White" ||
                    col.gameObject.tag == "Black" ||
                    col.gameObject.tag == "Red")
                {
                    actuallyOverlapping = true;
                    break;
                }
            }

            // Update state based on actual overlap
            if (actuallyOverlapping && !isCurrentlyOverlapping)
            {
                // Just started overlapping
                isCurrentlyOverlapping = true;
                if (!PhotonController.Instance.offlineMatch)
                {
                    photonView.RPC("SetOverlapVisuals", RpcTarget.All, true);
                }
                else
                {
                    SetOverlapVisuals(true);
                }
                isReadyShoot = false;
            }
            else if (!actuallyOverlapping && isCurrentlyOverlapping)
            {
                // No longer overlapping
                isCurrentlyOverlapping = false;
                if (!PhotonController.Instance.offlineMatch)
                {
                    photonView.RPC("SetOverlapVisuals", RpcTarget.All, false);
                }
                else
                {
                    SetOverlapVisuals(false);
                }
                isReadyShoot = true;
            }
        }
        void Update()
        {

            if (!touchEnabled)
            {
                isTouch = false;
                return;
            }
            isMoving = lastVel != Vector2.zero;

            if (photonView.IsMine && GameController.Instance.gameState == GameController.GameState.READY)
            {
                CheckForOverlaps();
            }

            if (photonView.IsMine)
            {
                if (rb.sharedMaterial == null)
                {
                    rb.sharedMaterial = physicMaterial;
                }

                // if (arrow.activeInHierarchy)
                // {
                //     arrow.transform.localScale =
                //         Vector2.MoveTowards(arrow.transform.localScale, arrowLocalScale, 5 * Time.deltaTime);
                // }

                if (!isTouch && GameController.Instance.gameState == GameController.GameState.READY)
                {
                    if (tutorialShowDelay > 0)
                    {
                        tutorialShowDelay -= 1 * Time.deltaTime;
                    }
                    else
                    {
                        if (!tutorial.activeInHierarchy && GameController.Instance.whichPlayer == GameController.WhichPlayer.ME && !tutorialShownBefore)
                        {
                            tutorial.SetActive(true);
                            tutorialShownBefore = true;
                        }
                    }

                    if (strikPuckShowDelay > 0)
                    {
                        strikPuckShowDelay -= 1 * Time.deltaTime;
                    }
                    else
                    {
                        if (!strikPuck.activeInHierarchy)
                        {
                            strikPuck.SetActive(true);
                        }
                    }
                }
            }
            else
            {
                tutorial.SetActive(false);

                if (rb.sharedMaterial != null)
                {
                    rb.sharedMaterial = null;
                }

                if (arrow.activeInHierarchy)
                {
                    arrow.transform.localScale = Vector2.MoveTowards(arrow.transform.localScale, arrowLocalScale,
                        50 * Time.deltaTime);
                }

                return;
            }

            if (isTouch)
            {
                endPos = Input.mousePosition;

                forceMultiplier = Mathf.InverseLerp(0, 300, (endPos - startPos).magnitude);

                if (forceMultiplier >= 0.2f)
                {
                    arrow.GetComponent<SpriteRenderer>().enabled = true;
                }
                else
                {
                    arrow.GetComponent<SpriteRenderer>().enabled = false;
                }

                arrow.transform.localScale = new Vector3(forceMultiplier * 2.3f, forceMultiplier * 2.3f, 1);

                float AngleRad = Mathf.Atan2(-(endPos.y - startPos.y), -(endPos.x - startPos.x));
                float AngleDeg = (180 / Mathf.PI) * AngleRad;
                transform.rotation = Quaternion.Euler(0, 0, AngleDeg);
            }
        }

        void FixedUpdate()
        {
            if (photonView.IsMine)
            {
                lastVel = rb.linearVelocity;
            }
        }

        public void OnMouseDown()
        {
            if (!touchEnabled) return;

            if (!photonView.IsMine ||
                GameController.Instance.gameState != GameController.GameState.READY ||
                GameController.Instance.whichPlayer != GameController.WhichPlayer.ME)
                return;

            if (isReadyShoot)
            {
                arrow.transform.localScale = Vector2.zero;
                arrow.SetActive(true);
                startPos = Input.mousePosition;
                endPos = startPos;

                isTouch = true;

                tutorial.SetActive(false);
                tutorialShowDelay = 2;
                strikPuck.SetActive(false);
                strikPuckShowDelay = 2;
            }
        }


        public void OnMouseUp()
        {
            if (!touchEnabled) return;

            if (!photonView.IsMine ||
                GameController.Instance.gameState != GameController.GameState.READY ||
                GameController.Instance.whichPlayer != GameController.WhichPlayer.ME)
                return;

            if (isReadyShoot)
            {
                arrow.SetActive(false);
                isTouch = false;

                if (forceMultiplier >= 0.2f)
                {
                    if (!PhotonController.Instance.offlineMatch)
                    {
                        photonView.RPC("Shoot", RpcTarget.AllBuffered);
                    }
                    else
                    {
                        Shoot();
                    }
                }

                chanceCount++;
            }
        }

        [PunRPC]
        void Shoot()
        {
            if (PhotonController.Instance.offlineMatch || (photonView.IsMine && isReadyShoot))
            {
                if (PhotonNetwork.LocalPlayer.ActorNumber == 1 || PhotonController.Instance.offlineMatch)
                {
                    rb.AddForce(transform.right * (forceMultiplier * force), ForceMode2D.Impulse);
                }
                else
                {
                    rb.AddForce(-transform.right * (forceMultiplier * force), ForceMode2D.Impulse);
                }
            }
            if (PhotonController.Instance.offlineMatch || isReadyShoot)
            {
                GameController.Instance.Shoot();
                tutorialShowDelay = 2;
                strikPuck.SetActive(false);
                strikPuckShowDelay = 2;
                isCurrentlyOverlapping = false;
            }
        }

        private bool isCurrentlyOverlapping = false;

        // Combined overlap detection with hole fallback processing
        private void OnTriggerStay2D(Collider2D collision)
        {
            // First: handle hole fallback (ensures pocketing if already inside when allowed)
            if (collision.CompareTag("Hole"))
            {
                if (GameController.Instance.allowPocketing)
                {
                    OnTriggerEnter2D(collision);
                }
                return;
            }

            // Then: existing overlap logic for coins
            /* if (collision.gameObject.tag == "White" || collision.gameObject.tag == "Black" || collision.gameObject.tag == "Red")
             {
                 if (!isCurrentlyOverlapping)
                 {
                     isCurrentlyOverlapping = true;
                     photonView.RPC("SetOverlapVisuals", RpcTarget.All, true);
                     isReadyShoot = false;
                 }
             }*/
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            if (collision.gameObject.tag == "White" || collision.gameObject.tag == "Black" || collision.gameObject.tag == "Red")
            {
                if (isCurrentlyOverlapping)
                {
                    isCurrentlyOverlapping = false;
                    if (!PhotonController.Instance.offlineMatch)
                    {
                        photonView.RPC("SetOverlapVisuals", RpcTarget.All, false);
                    }
                    else
                    {
                        SetOverlapVisuals(false);
                    }
                    isReadyShoot = true;
                }
            }
        }

        [PunRPC]
        void SetOverlapVisuals(bool isOverlapping)
        {
            if (isOverlapping)
            {
                if (!strikPuck.activeInHierarchy)
                {
                    strikPuck.SetActive(true);
                }
                strikPuck.GetComponent<SpriteRenderer>().color = new Color32(255, 69, 74, 255);
                spriteRenderer.color = new Color32(255, 255, 255, 50);
            }
            else
            {
                if (strikPuck.activeInHierarchy)
                {
                    strikPuck.GetComponent<SpriteRenderer>().color = new Color32(255, 255, 255, 255);
                }
                spriteRenderer.color = new Color32(255, 255, 255, 255);
            }
        }

        public void ResetPosition()
        {
            if (photonView.IsMine || PhotonController.Instance.offlineMatch)
            {
                EnableTouchControls();
                if (!PhotonController.Instance.offlineMatch)
                {
                    if (PhotonNetwork.LocalPlayer.ActorNumber != 1)
                    {
                        transform.localPosition = -puckStartPos;
                        transform.localScale = new Vector3(-0.4f, 0.4f, 1);
                    }
                    else
                    {
                        transform.localPosition = puckStartPos;
                        transform.localScale = new Vector3(0.4f, 0.4f, 1);
                    }
                }
                else
                {
                    transform.localPosition = puckStartPos;
                    transform.localScale = new Vector3(0.4f, 0.4f, 1);
                }


                transform.rotation = Quaternion.Euler(0, 0, 90);

                Invoke("EnableCollider", 0.2f);

                isReadyShoot = true;
            }
            //if (photonView.IsMine)
            //{
            //    transform.localPosition = puckStartPos;
            //    if (!isCurrentlyOverlapping)
            //    {
            //        Invoke("EnableCollider", 0.2f);
            //    }
            //}
        }

        public void EnablePuckPhysic()
        {
            float t = photonView.IsMine ? 0.3f : 1f;

            if (PhotonController.Instance.offlineMatch)
            {
                    LeanTween.value(0, 1, t).setOnComplete(() =>
                {
                    GetComponent<CircleCollider2D>().enabled = true;
                    //collider.isTrigger = false;
                    rb.linearVelocity = Vector2.zero;
                    rb.simulated = true;
                    spriteRenderer.color = new Color32(255, 255, 255, 255);
                    lastVel = Vector2.zero;
                    isMoving = false;
                });
            }
            else
            {
                if (photonView.IsMine)
                {
                    LeanTween.value(0, 1, t).setOnComplete(() => {
                        GetComponent<CircleCollider2D>().enabled = true;
                        collider.isTrigger = false;
                        rb.linearVelocity = Vector2.zero;
                        rb.simulated = true;
                        spriteRenderer.color = new Color32(255, 255, 255, 255);
                        lastVel = Vector2.zero;
                        isMoving = false;
                    });
                }
                else
                {
                    LeanTween.value(0, 1, t).setOnComplete(() => {
                        GetComponent<CircleCollider2D>().enabled = true;
                        rb.linearVelocity = Vector2.zero;
                        rb.simulated = true;
                        spriteRenderer.color = new Color32(255, 255, 255, 255);
                        lastVel = Vector2.zero;
                        isMoving = false;
                    });
                }
            }            
        }

        public void ShootBot()
        {
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(ShootBotCoroutine());
            }
        }

        IEnumerator ShootBotCoroutine()
        {
            collider.isTrigger = true;
            SetupStriker();
            if (GameController.Instance.hasBroken)
            {
                yield return StartCoroutine(ScanBoard());
            }
            else
            {
                selectedShot = breakShots[Random.Range(0, breakShots.Count)];

                GetComponent<SpriteRenderer>().enabled = true;
                strikPuck.GetComponent<SpriteRenderer>().enabled = true;
                yield return new WaitForSeconds(0.1f);
                clone.SetActive(false);
            }

            if (selectedShot == null) yield break;
            selectedShot.PrintData();

            float waitTime;
            float roll = Random.value;
            if (roll < 0.6f)
                waitTime = Random.Range(0.5f, 2f);
            else if (roll < 0.9f)
                waitTime = Random.Range(2f, 5f);
            else
                waitTime = Random.Range(5f, 10f);

            yield return new WaitForSeconds(waitTime);

            StartCoroutine(MoveDiscToShotPosition(selectedShot));
        }

        IEnumerator FakeLagCoroutine()
        {
            isShotDone = false;
            while (!isShotDone)
            {
                // Freeze for a bit
                Time.timeScale = 0f;
                float freezeTime = Random.Range(.09f, .12f);;
                yield return new WaitForSecondsRealtime(freezeTime);

                // Run for a bit
                Time.timeScale = 2f;
                float runTime = Random.Range(.09f, .12f);
                yield return new WaitForSecondsRealtime(runTime);
            }
            Time.timeScale = 1f;
        }

        void SetupStriker()
        {
            if (GameController.Instance.hasBroken)
            {
                clone.SetActive(true);
                GetComponent<SpriteRenderer>().enabled = false;
                strikPuck.GetComponent<SpriteRenderer>().enabled = false;
            }
            EnablePuckPhysic();
            //collider.isTrigger = true;
            arrowLocalScale = Vector2.zero;
            arrow.transform.localScale = Vector2.zero;
            transform.localPosition = -puckStartPos;
            transform.localScale = new Vector3(-0.4f, 0.4f, 1);
            forceMultiplier = 0;
            correctPuckHit = false;
            correctWallHit = false;
            arrow.SetActive(true);
        }

        IEnumerator ScanBoard()
        {
            Vector2 defaultPos = transform.localPosition;
            const float nearZeroThreshold = 15f;
            const float step = 0.05f;

            WallInfo[] walls = GatherWalls();
            Debug.Log($"[WALLS] Total walls gathered: {walls.Length}");
            foreach (WallInfo wall in walls)
            {
                Debug.Log($"[WALLS] IsVertical={wall.IsVertical}  Position={wall.Position}  Min={wall.Min}  Max={wall.Max}");
            }
            Puck[] blackPucks = GetActiveBlackPucks();
            Transform[] allHoles = GetAllHoles();

            if (blackPucks.Length == 0 || allHoles.Length == 0)
            {
                Debug.LogWarning("BotAI: no black pucks or holes found – aborting.");
                rb.simulated = true;
                yield break;
            }

            HoleLayout layout = CategorizeHoles(allHoles);
            float strikerRadius = GetComponent<CircleCollider2D>().radius * Mathf.Abs(transform.localScale.x);
            float minX = GameController.Instance.playerPuckMinX;
            float maxX = GameController.Instance.playerPuckMaxX;
            float scanY = transform.position.y;

            (bool redOnBoard, bool mustTryRed, bool shouldTryRed, float redPuckRadius) =
                EvaluateRedPuckStatus(blackPucks);

            // ── Debug line renderers ──────────────────────────────────────────────
            LineRenderer lr1C = BotCreateLine("Bot_L1_Center", new Color(0.1f, 1f, 0.1f), .02f);
            LineRenderer lr2C = BotCreateLine("Bot_L2_Center", new Color(0.1f, 0.9f, 1f), .02f);
            LineRenderer lr1L = BotCreateLine("Bot_L1_Left", new Color(1f, 1f, 0f), .02f);
            LineRenderer lr1R = BotCreateLine("Bot_L1_Right", new Color(1f, 1f, 0f), .02f);
            LineRenderer lr2L = BotCreateLine("Bot_L2_Left", new Color(1f, 0.5f, 0f), .02f);
            LineRenderer lr2R = BotCreateLine("Bot_L2_Right", new Color(1f, 0.5f, 0f), .02f);

            // ── Scan state ────────────────────────────────────────────────────────
            var blackState = new BlackScanState();
            var redState = new RedScanState();

            // bool allowCannonShots = GameController.Instance.turnCount > 3;
            bool allowCannonShots = true;

            // ── PHASE 1 – Horizontal scan ─────────────────────────────────────────
            for (float x = minX; x <= maxX; x += step)
            {
                Vector2 testPos = new Vector2(x, scanY);
                transform.position = testPos;

                if (IsOverlappingPuck(testPos, strikerRadius)) continue;

                if (!mustTryRed)
                {
                    ScanBlackPucksAtPosition(testPos, blackPucks, layout, strikerRadius,
                                            nearZeroThreshold, blackState,
                                            lr1C, lr2C, lr1L, lr1R, lr2L, lr2R);
                    if (allowCannonShots)
                    {
                        ScanCannonBlackAtPosition(testPos, blackPucks, layout, strikerRadius, blackState.Cannon);
                        if(GameController.Instance.turnCount > 5)
                        {
                            ScanBankCannonAtPosition(testPos, blackPucks, layout, strikerRadius, walls, blackState.BankCannon); // ← add
                        }
                    }
                    if(GameController.Instance.turnCount > 5)
                    {
                        ScanBankShotsAtPosition(testPos, blackPucks, layout, strikerRadius, walls, blackState);
                    }
                }

                if (shouldTryRed)
                {
                    ScanRedPuckAtPosition(testPos, layout, strikerRadius, redPuckRadius, nearZeroThreshold, redState);
                    if (allowCannonShots)
                    {
                        ScanCannonRedAtPosition(testPos, layout, strikerRadius, redPuckRadius, redState.Cannon);
                    }
                    ScanBankShotsRedAtPosition(testPos, layout, strikerRadius, redPuckRadius, walls, redState); // ← add
                }
            }

            // ── Post-scan: discard opportunistic red shot if no black follow-up ──
            if (redState.DirectShot.Valid && !mustTryRed &&
                !HasEasyBlackShotExists(blackPucks, allHoles, minX, maxX, scanY, strikerRadius))
            {
                redState.DirectShot.Valid = false;
                Debug.Log("[BotAI] Red shot discarded — no easy black follow-up found.");
            }

            if (mustTryRed && !redState.DirectShot.Valid && !redState.Fallback.Valid)
                Debug.LogWarning("[BotAI] mustTryRed=true but no red shot found at all.");

            // ── PHASE 2 – Select best shot ────────────────────────────────────────
            FinalShotInfo final = SelectFinalShot(blackState, redState, mustTryRed, shouldTryRed);

            if (final == null)
            {
                if (!TryEmergencyShot(blackPucks, minX, maxX))
                {
                    Debug.LogError("[BotAI] Emergency fallback also failed. Aborting.");
                    transform.localPosition = defaultPos;
                    rb.simulated = true;
                    yield break;
                }
                transform.localPosition = defaultPos;
                GetComponent<SpriteRenderer>().enabled = true;
                strikPuck.GetComponent<SpriteRenderer>().enabled = true;
                yield return new WaitForSeconds(0.1f);
                clone.SetActive(false);
                yield break;
            }

            // ── Show final debug lines ────────────────────────────────────────────
            BotSetLine(lr1C, final.PuckPos, final.HolePos);
            BotSetLine(lr2C, final.StrikerPos, final.PuckPos);
            BotSetLine(lr1L, final.Lines.L1LS, final.Lines.L1LE);
            BotSetLine(lr1R, final.Lines.L1RS, final.Lines.L1RE);
            BotSetLine(lr2L, final.Lines.L2LS, final.Lines.L2LE);
            BotSetLine(lr2R, final.Lines.L2RS, final.Lines.L2RE);

            yield return new WaitForSeconds(1.5f);

            // ── PHASE 3 – Build selectedShot ──────────────────────────────────────
            final = VerifyAndFixOverlap(final, blackState, redState, strikerRadius);
            selectedShot = BuildFinalShot(final, blackState, redState, blackPucks,
                                        strikerRadius, redPuckRadius, redOnBoard, layout);

            LogFinalShot(final, blackState, redState);

            // ── Cleanup ───────────────────────────────────────────────────────────
            transform.localPosition = defaultPos;
            GetComponent<SpriteRenderer>().enabled = true;
            strikPuck.GetComponent<SpriteRenderer>().enabled = true;
            yield return new WaitForSeconds(0.1f);
            clone.SetActive(false);
        }

        private Puck[] GetActiveBlackPucks() =>
            GameController.Instance.allPucks
                .Where(x => x.CompareTag("Black") && x.gameObject.activeInHierarchy && !x.isPocketed)
                .ToArray();

        private Puck[] GetActivePucks() =>
            GameController.Instance.allPucks
                .Where(x => x.gameObject.activeInHierarchy && !x.isPocketed)
                .ToArray();

        private Transform[] GetAllHoles() =>
            GameObject.FindGameObjectsWithTag("Hole")
                .Select(h => h.transform)
                .ToArray();

        private HoleLayout CategorizeHoles(Transform[] holes)
        {
            float midX = holes.Average(h => h.position.x);

            Transform[] left = holes.Where(h => h.position.x <= midX).OrderBy(h => h.position.y).ToArray();
            Transform[] right = holes.Where(h => h.position.x > midX).OrderBy(h => h.position.y).ToArray();

            var layout = new HoleLayout
            {
                All = holes,
                MidX = midX,
                TopLeft = left.Length > 0 ? left[left.Length - 1] : null,
                BottomLeft = left.Length > 0 ? left[0] : null,
                TopRight = right.Length > 0 ? right[right.Length - 1] : null,
                BottomRight = right.Length > 0 ? right[0] : null,
            };

            layout.Line12Left = (layout.TopLeft != null && layout.BottomLeft != null)
                ? Vector2.Distance(layout.TopLeft.position, layout.BottomLeft.position) : float.MaxValue;
            layout.Line12Right = (layout.TopRight != null && layout.BottomRight != null)
                ? Vector2.Distance(layout.TopRight.position, layout.BottomRight.position) : float.MaxValue;

            return layout;
        }

        private void ScanBlackPucksAtPosition(
            Vector2 testPos, Puck[] blackPucks, HoleLayout layout,
            float strikerRadius, float nearZeroThreshold,
            BlackScanState state,
            LineRenderer lr1C, LineRenderer lr2C,
            LineRenderer lr1L, LineRenderer lr1R,
            LineRenderer lr2L, LineRenderer lr2R)
        {
            foreach (Puck puck in blackPucks)
            {
                int bestHoleThisStep = -1;
                float bestAngle4 = float.MaxValue;
                Vector2 bestL1CS = default, bestL1CE = default;
                Vector2 bestL2CS = default, bestL2CE = default;
                ShotLines bestLinesThisStep = default;

                for (int hi = 0; hi < layout.All.Length; hi++)
                {
                    Transform hole = layout.All[hi];
                    Vector2 puckPos = puck.transform.position;
                    Vector2 holePos = hole.position;

                    if (!IsHoleEligibleForBlackPuck(testPos, puckPos, holePos, hole, layout, strikerRadius, state, puck))
                    {
                        continue;
                    }

                    // ── Geometry ──────────────────────────────────────────────────
                    Vector2 line1 = holePos - puckPos;
                    float dist1 = line1.magnitude;
                    Vector2 line1Dir = line1.normalized;
                    Vector2 line2 = puckPos - testPos;
                    float dist2 = line2.magnitude;
                    Vector2 line2Dir = line2.normalized;

                    if (dist1 >= Vector2.Distance(testPos, holePos)) continue;

                    float puckRadius = puck.GetComponent<CircleCollider2D>().radius * Mathf.Abs(puck.transform.localScale.x);
                    float combinedR = puckRadius + strikerRadius;

                    Vector2 perp1 = new Vector2(-line1Dir.y, line1Dir.x);
                    Vector2 perp2 = new Vector2(-line2Dir.y, line2Dir.x);

                    ShotLines lines = ShotLines.Make(
                        puckPos + perp1 * puckRadius, holePos + perp1 * puckRadius,
                        puckPos - perp1 * puckRadius, holePos - perp1 * puckRadius,
                        testPos + perp2 * strikerRadius, puckPos + perp2 * strikerRadius,
                        testPos - perp2 * strikerRadius, puckPos - perp2 * strikerRadius);

                    // ── Angles ────────────────────────────────────────────────────
                    float angle1Up = Vector2.Angle(Vector2.up, line1);
                    float angle1Down = Vector2.Angle(Vector2.down, line1);
                    bool isNearZeroUp = angle1Up < nearZeroThreshold;
                    bool isNearZeroDown = angle1Down < nearZeroThreshold;
                    bool isNearZeroAny = isNearZeroUp || isNearZeroDown;

                    float angle2 = Mathf.Atan2(combinedR, dist2) * Mathf.Rad2Deg;
                    if (angle2 < 0.001f) continue;

                    float scaleFactor = angle1Up > 0.001f ? angle2 / angle1Up : 0f;
                    float angle3 = Vector2.Angle(line1, line2);
                    float angle4 = Mathf.Clamp(angle3 * scaleFactor, 0f, angle2);

                    // ── Near-zero qualification ────────────────────────────────────
                    bool nearZeroTopOpp = IsNearZeroTopOppositeQualified(testPos, puckPos, holePos, hole, dist1, layout, isNearZeroAny);
                    bool nearZeroBottomSame = IsNearZeroBottomSameQualified(testPos, holePos, hole, layout, isNearZeroAny);

                    bool forcedTopFallback = puckPos.y < 1.15f &&
                                            (hole == layout.TopLeft || hole == layout.TopRight);

                    // ── Shoot direction ────────────────────────────────────────────
                    Vector2 shootDir = ComputeBlackShootDir(testPos, puckPos, holePos, line1Dir, line2Dir,
                                                            combinedR, angle4, nearZeroTopOpp,
                                                            nearZeroBottomSame, forcedTopFallback);

                    // ── Obstacle checks ────────────────────────────────────────────
                    float startOffset = strikerRadius + 0.05f;

                    Vector2 perp2Shoot = new Vector2(-shootDir.y, shootDir.x);

                    bool centerBlocked1 = CheckRay(puckPos + line1Dir * startOffset, line1Dir,
                        dist1 - startOffset, puck.gameObject, gameObject);
                    bool centerBlocked2 = CheckRay(testPos + shootDir * startOffset, shootDir,
                        dist2 - startOffset, puck.gameObject, gameObject);
                    bool l1LBlocked = CheckRay(lines.L1LS + line1Dir * startOffset, line1Dir,
                        dist1 - startOffset, puck.gameObject, gameObject);
                    bool l1RBlocked = CheckRay(lines.L1RS + line1Dir * startOffset, line1Dir,
                        dist1 - startOffset, puck.gameObject, gameObject);
                    bool l2LBlocked = CheckRay(testPos + perp2Shoot * strikerRadius + shootDir * startOffset, shootDir,
                        dist2 - startOffset, puck.gameObject, gameObject);
                    bool l2RBlocked = CheckRay(testPos - perp2Shoot * strikerRadius + shootDir * startOffset, shootDir,
                        dist2 - startOffset, puck.gameObject, gameObject);

                    bool l1FullyClear = !centerBlocked1 && !l1LBlocked && !l1RBlocked;
                    bool l1ParallelBlocked = l1LBlocked || l1RBlocked;
                    bool l2ParallelBlocked = l2LBlocked || l2RBlocked;

                    if (!l1FullyClear)
                    {
                        continue;
                    }

                    // ── Fallback (always record if line1 is clear, ignoring line2) ─
                    bool allowFallback = ShouldAllowFallback(puckPos, hole, layout, state.FoundUsableTopFallbackCandidate);
                    if (allowFallback)
                    {
                        if (state.Fallback.TryUpdate(angle4, testPos, shootDir, puckPos, holePos, hole.name, lines))
                        {
                            CheckRayGetBlocker(testPos + line2Dir * startOffset, line2Dir,
                                dist2 - startOffset, puck.gameObject, gameObject,
                                out state.FallbackBlockerName);
                        }
                    }

                    if (centerBlocked1 || centerBlocked2 || l2ParallelBlocked) continue;

                    // ── Track valid bottom shots ───────────────────────────────────
                    if (puckPos.y < 1.15f && (hole == layout.BottomLeft || hole == layout.BottomRight) && !l1ParallelBlocked)
                        state.HasValidBottomShot = true;

                    // ── Update step-best for debug lines ──────────────────────────
                    if (angle4 < bestAngle4)
                    {
                        bestAngle4 = angle4;
                        bestHoleThisStep = hi;
                        bestL1CS = puckPos; bestL1CE = holePos;
                        bestL2CS = testPos; bestL2CE = puckPos;
                        bestLinesThisStep = lines;
                    }

                    // ── Update near-zero / high-puck / normal best ─────────────────
                    RecordBestBlackShot(testPos, puckPos, holePos, hole, shootDir, angle2, angle4,
                                        angle1Up, angle1Down, scaleFactor, angle3,
                                        isNearZeroUp, isNearZeroDown,
                                        nearZeroTopOpp, nearZeroBottomSame,
                                        forcedTopFallback, lines, layout, state);
                }

                // ── Update debug line renderers ────────────────────────────────────
                if (bestHoleThisStep >= 0)
                {
                    BotSetLine(lr1C, bestL1CS, bestL1CE);
                    BotSetLine(lr2C, bestL2CS, bestL2CE);
                    BotSetLine(lr1L, bestLinesThisStep.L1LS, bestLinesThisStep.L1LE);
                    BotSetLine(lr1R, bestLinesThisStep.L1RS, bestLinesThisStep.L1RE);
                    BotSetLine(lr2L, bestLinesThisStep.L2LS, bestLinesThisStep.L2LE);
                    BotSetLine(lr2R, bestLinesThisStep.L2RS, bestLinesThisStep.L2RE);
                }
                else
                {
                    BotSetLine(lr1C, testPos, testPos); BotSetLine(lr2C, testPos, testPos);
                    BotSetLine(lr1L, testPos, testPos); BotSetLine(lr1R, testPos, testPos);
                    BotSetLine(lr2L, testPos, testPos); BotSetLine(lr2R, testPos, testPos);
                }
            }
        }

        private void ScanBankShotsAtPosition(
            Vector2 testPos, Puck[] blackPucks, HoleLayout layout,
            float strikerRadius, WallInfo[] walls, BlackScanState state)
        {
            foreach (Puck puck in blackPucks)
            {
                Vector2 puckPos = puck.transform.position;
                float puckRadius = puck.GetComponent<CircleCollider2D>().radius
                                    * Mathf.Abs(puck.transform.localScale.x);
                float combinedR = puckRadius + strikerRadius;
                float off = strikerRadius + 0.05f;

                for (int hi = 0; hi < layout.All.Length; hi++)
                {
                    Transform hole = layout.All[hi];
                    Vector2 holePos = hole.position;

                    // ── Line1: puck→hole must be clear ───────────────────────────────
                    Vector2 line1 = holePos - puckPos;
                    float dist1 = line1.magnitude;
                    Vector2 line1Dir = line1.normalized;

                    bool centerBlocked1 = CheckRayPucksOnly(puckPos + line1Dir * off, line1Dir,
                                            dist1 - off, puck.gameObject, gameObject);
                    bool l1LBlocked, l1RBlocked;
                    {
                        Vector2 perp1 = new Vector2(-line1Dir.y, line1Dir.x);
                        l1LBlocked = CheckRayPucksOnly(puckPos + perp1 * puckRadius + line1Dir * off,
                                            line1Dir, dist1 - off, puck.gameObject, gameObject);
                        l1RBlocked = CheckRayPucksOnly(puckPos - perp1 * puckRadius + line1Dir * off,
                                            line1Dir, dist1 - off, puck.gameObject, gameObject);
                    }
                    if (centerBlocked1 || l1LBlocked || l1RBlocked) continue;

                    // ── Angle4 score ─────────────────────────────────────────────────
                    Vector2 line2 = puckPos - testPos;
                    float dist2 = line2.magnitude;
                    float angle1Up = Vector2.Angle(Vector2.up, line1);
                    float angle2 = Mathf.Atan2(combinedR, dist2) * Mathf.Rad2Deg;
                    if (angle2 < 0.001f) continue;
                    float scaleFactor = angle1Up > 0.001f ? angle2 / angle1Up : 0f;
                    float angle3 = Vector2.Angle(line1, line2);
                    float angle4 = Mathf.Clamp(angle3 * scaleFactor, 0f, angle2);

                    foreach (WallInfo wall in walls)
                    {
                        // ── Hole restrictions per wall type ──────────────────────────
                        if (!wall.IsVertical)
                        {
                            if (hole != layout.TopLeft && hole != layout.TopRight) continue;
                            if (testPos.x > puckPos.x && hole != layout.TopLeft) continue;
                            if (testPos.x < puckPos.x && hole != layout.TopRight) continue;
                        }
                        // Vertical walls: all holes allowed

                        // ── Compute bank point ────────────────────────────────────────
                        Vector2 bankPoint;
                        if (!wall.IsVertical)
                        {
                            Vector2 mirroredPos = new Vector2(testPos.x, 2f * wall.Position - testPos.y);
                            Vector2 dir2Bank = puckPos - mirroredPos;
                            if (Mathf.Abs(dir2Bank.y) < 0.001f) continue;
                            float t = (wall.Position - mirroredPos.y) / dir2Bank.y;
                            if (t <= 0f || t >= 1f) continue;
                            bankPoint = mirroredPos + dir2Bank * t;
                            if (bankPoint.x < wall.Min || bankPoint.x > wall.Max) continue;
                            if (bankPoint.x < -1.8f || bankPoint.x > 1.8f) continue;
                            float wallCenterX = (wall.Min + wall.Max) * 0.5f;
                            bankPoint = new Vector2(Mathf.Lerp(wallCenterX, bankPoint.x, bankAngleBias), bankPoint.y);
                            if (testPos.y <= wall.Position) continue;
                            if (Mathf.Abs(testPos.x - puckPos.x) < 0.5f) continue;
                        }
                        else
                        {
                            Vector2 mirroredPos = new Vector2(2f * wall.Position - testPos.x, testPos.y);
                            Vector2 dir2Bank = puckPos - mirroredPos;
                            if (Mathf.Abs(dir2Bank.x) < 0.001f) continue;
                            float t = (wall.Position - mirroredPos.x) / dir2Bank.x;
                            if (t <= 0f || t >= 1f) continue;
                            bankPoint = mirroredPos + dir2Bank * t;
                            if (bankPoint.y < wall.Min || bankPoint.y > wall.Max) continue;
                            if (bankPoint.y < -2.0f || bankPoint.y > 2.0f) continue;
                            float wallCenterY = (wall.Min + wall.Max) * 0.5f;
                            bankPoint = new Vector2(bankPoint.x, Mathf.Lerp(wallCenterY, bankPoint.y, bankAngleBias));
                            // Disc and puck must be on same side of wall
                            if ((testPos.x - wall.Position) * (puckPos.x - wall.Position) <= 0) continue;
                            if (Mathf.Abs(testPos.y - puckPos.y) < 0.5f) continue;
                        }

                        Vector2 toBankDir = (bankPoint - testPos).normalized;
                        float toBankDist = Vector2.Distance(testPos, bankPoint);
                        Vector2 bankPerp = new Vector2(-toBankDir.y, toBankDir.x);

                        bool ray1Center = CheckRayPucksOnly(testPos + toBankDir * off, toBankDir, toBankDist - off, puck.gameObject, gameObject);
                        bool ray1Left = CheckRayPucksOnly(testPos + bankPerp * strikerRadius + toBankDir * off, toBankDir, toBankDist - off, puck.gameObject, gameObject);
                        bool ray1Right = CheckRayPucksOnly(testPos - bankPerp * strikerRadius + toBankDir * off, toBankDir, toBankDist - off, puck.gameObject, gameObject);
                        if (ray1Center || ray1Left || ray1Right) continue;

                        Vector2 bankToPuckDir = (puckPos - bankPoint).normalized;
                        float bankToPuckDist = Vector2.Distance(bankPoint, puckPos);
                        Vector2 bankToPuckPerp = new Vector2(-bankToPuckDir.y, bankToPuckDir.x);

                        bool ray2Center = CheckRayPucksOnly(bankPoint + bankToPuckDir * off, bankToPuckDir, bankToPuckDist - off, puck.gameObject, gameObject);
                        bool ray2Left = CheckRayPucksOnly(bankPoint + bankToPuckPerp * strikerRadius + bankToPuckDir * off, bankToPuckDir, bankToPuckDist - off, puck.gameObject, gameObject);
                        bool ray2Right = CheckRayPucksOnly(bankPoint - bankToPuckPerp * strikerRadius + bankToPuckDir * off, bankToPuckDir, bankToPuckDist - off, puck.gameObject, gameObject);
                        if (ray2Center || ray2Left || ray2Right) continue;

                        // ── Hole-in-path check ────────────────────────────────────────
                        Vector2 b2pDir = (puckPos - bankPoint).normalized;
                        float b2pDist = Vector2.Distance(bankPoint, puckPos);
                        Vector2 b2pPerp = new Vector2(-b2pDir.y, b2pDir.x);
                        bool holeC = CheckRayForHoles(bankPoint + b2pDir * off, b2pDir, b2pDist - off);
                        bool holeL = CheckRayForHoles(bankPoint + b2pPerp * strikerRadius + b2pDir * off, b2pDir, b2pDist - off);
                        bool holeR = CheckRayForHoles(bankPoint - b2pPerp * strikerRadius + b2pDir * off, b2pDir, b2pDist - off);
                        if (holeC || holeL || holeR) continue;

                        Vector2 perp1 = new Vector2(-line1Dir.y, line1Dir.x);
                        Vector2 perp2 = new Vector2(-toBankDir.y, toBankDir.x);
                        ShotLines lines = ShotLines.Make(
                            puckPos + perp1 * puckRadius, holePos + perp1 * puckRadius,
                            puckPos - perp1 * puckRadius, holePos - perp1 * puckRadius,
                            testPos + perp2 * strikerRadius, bankPoint + perp2 * strikerRadius,
                            testPos - perp2 * strikerRadius, bankPoint - perp2 * strikerRadius);

                        if (state.BankShot.TryUpdate(angle4, testPos, toBankDir, puckPos, holePos, hole.name, lines))
                        {
                            state.A1Up = angle1Up;
                            state.A2 = angle2;
                            state.A3 = angle3;
                            state.Scale = scaleFactor;
                            Debug.Log($"[BotAI][BANK] BankPt={bankPoint}  Puck={puck.name}  A4={angle4:F2}°  Wall=IsVertical:{wall.IsVertical}");
                        }
                    }
                }
            }
        }

        private void ScanBankCannonAtPosition(
            Vector2 testPos, Puck[] blackPucks, HoleLayout layout,
            float strikerRadius, WallInfo[] walls, CannonCandidate result)
        {
            float off = strikerRadius + 0.05f;
            foreach (Puck targetPuck in blackPucks)
            {
                Vector2 targetPos = targetPuck.transform.position;
                float targetR = targetPuck.GetComponent<CircleCollider2D>().radius * Mathf.Abs(targetPuck.transform.localScale.x);

                for (int hi = 0; hi < layout.All.Length; hi++)
                {
                    Transform hole = layout.All[hi];
                    Vector2 holePos = hole.position;
                    if (!IsLine1ClearForCannon(targetPos, targetR, holePos, strikerRadius, targetPuck.gameObject)) continue;
                    foreach (Puck blockerPuck in GetActivePucks())
                    {
                        if (blockerPuck == targetPuck) continue;

                        Vector2 blockerPos = blockerPuck.transform.position;
                        float blockerR = blockerPuck.GetComponent<CircleCollider2D>().radius * Mathf.Abs(blockerPuck.transform.localScale.x);

                        Vector2 toHole = (holePos - targetPos).normalized;
                        Vector2 b2tDir = (targetPos - blockerPos).normalized;
                        float alignAngle = Vector2.Angle(b2tDir, toHole);
                        if (alignAngle > 50f) continue;

                        if (targetPos.y <= blockerPos.y) continue;
                        if (hole != layout.TopLeft && hole != layout.TopRight) continue;

                        if (!IsLine1ClearForCannon(blockerPos, blockerR, targetPos, strikerRadius, targetPuck.gameObject)) continue;

                        float combinedRS = strikerRadius + blockerR;
                        Vector2 ghostBall = blockerPos - b2tDir * combinedRS;

                        foreach (WallInfo wall in walls)
                        {
                            Vector2 bankPoint;
                            if (!wall.IsVertical)
                            {
                                Vector2 mirroredPos = new Vector2(testPos.x, 2f * wall.Position - testPos.y);
                                Vector2 dirToGhost = ghostBall - mirroredPos;
                                if (Mathf.Abs(dirToGhost.y) < 0.001f) continue;
                                float t = (wall.Position - mirroredPos.y) / dirToGhost.y;
                                if (t <= 0f || t >= 1f) continue;
                                bankPoint = mirroredPos + dirToGhost * t;
                                if (bankPoint.x < wall.Min || bankPoint.x > wall.Max) continue;
                                if (bankPoint.x < -1.8f || bankPoint.x > 1.8f) continue;
                                float wallCenterX = (wall.Min + wall.Max) * 0.5f;
                                bankPoint = new Vector2(Mathf.Lerp(wallCenterX, bankPoint.x, bankAngleBias), bankPoint.y);
                                if (testPos.y <= wall.Position) continue;
                                if (Mathf.Abs(testPos.x - ghostBall.x) < 0.5f) continue;
                            }
                            else
                            {
                                Vector2 mirroredPos = new Vector2(2f * wall.Position - testPos.x, testPos.y);
                                Vector2 dirToGhost = ghostBall - mirroredPos;
                                if (Mathf.Abs(dirToGhost.x) < 0.001f) continue;
                                float t = (wall.Position - mirroredPos.x) / dirToGhost.x;
                                if (t <= 0f || t >= 1f) continue;
                                bankPoint = mirroredPos + dirToGhost * t;
                                if (bankPoint.y < wall.Min || bankPoint.y > wall.Max) continue;
                                if (bankPoint.y < -2.0f || bankPoint.y > 2.0f) continue;
                                float wallCenterY = (wall.Min + wall.Max) * 0.5f;
                                bankPoint = new Vector2(bankPoint.x, Mathf.Lerp(wallCenterY, bankPoint.y, bankAngleBias));
                                if ((testPos.x - wall.Position) * (ghostBall.x - wall.Position) <= 0) continue;
                                if (Mathf.Abs(testPos.y - ghostBall.y) < 0.5f) continue;

                                // ── Ghost ball must be on the board side of this wall ────────────
                                bool isRightWall = wall.Position > 0;
                                if (isRightWall && ghostBall.x > wall.Position) continue;
                                if (!isRightWall && ghostBall.x < wall.Position) continue;
                                // ────────────────────────────────────────────────────────────────

                                // ── Disc must be on the same side as the wall ────────────────────
                                if (isRightWall && testPos.x < 0) continue;
                                if (!isRightWall && testPos.x > 0) continue;

                                if (hole == layout.TopLeft || hole == layout.TopRight) continue;

                                if (isRightWall && targetPos.x > blockerPos.x) continue;
                                if (!isRightWall && targetPos.x < blockerPos.x) continue;
                            }
                            // Debug.Log($"[LEFT-WALL] Passed geometry! testPos={testPos} bankPt={bankPoint}  blocker={blockerPuck.name}  target={targetPuck.name}  wallPos={wall.Position}  wallIsVertical={wall.IsVertical}");

                            if (!wall.IsVertical)
                            {
                                if (bankPoint.x > blockerPos.x && targetPos.x > blockerPos.x + 0.05f) continue;
                                if (bankPoint.x < blockerPos.x && targetPos.x < blockerPos.x - 0.05f) continue;
                            }
                            // else
                            // {
                            //     if (bankPoint.y > blockerPos.y && targetPos.y > blockerPos.y + 0.05f) continue;
                            //     if (bankPoint.y < blockerPos.y && targetPos.y < blockerPos.y - 0.05f) continue;
                            // }

                            Vector2 toBankDir = (bankPoint - testPos).normalized;
                            float toBankDist = Vector2.Distance(testPos, bankPoint);
                            Vector2 bankPerp = new Vector2(-toBankDir.y, toBankDir.x);

                            bool r1C = CheckRayPucksOnly(testPos + toBankDir * off, toBankDir, toBankDist - off, blockerPuck.gameObject, gameObject);
                            bool r1L = CheckRayPucksOnly(testPos + bankPerp * strikerRadius + toBankDir * off, toBankDir, toBankDist - off, blockerPuck.gameObject, gameObject);
                            bool r1R = CheckRayPucksOnly(testPos - bankPerp * strikerRadius + toBankDir * off, toBankDir, toBankDist - off, blockerPuck.gameObject, gameObject);
                            Debug.Log($"[LEFT-WALL] r1 C={r1C} L={r1L} R={r1R}");
                            if (r1C || r1L || r1R) continue;

                            Vector2 bankToGhostDir = (ghostBall - bankPoint).normalized;
                            float bankToGhostDist = Vector2.Distance(bankPoint, ghostBall);
                            Vector2 bankToGhostPerp = new Vector2(-bankToGhostDir.y, bankToGhostDir.x);

                            bool r2C = CheckRayPucksOnly(bankPoint + bankToGhostDir * off, bankToGhostDir, bankToGhostDist - off, blockerPuck.gameObject, gameObject);
                            bool r2L = CheckRayPucksOnly(bankPoint + bankToGhostPerp * strikerRadius + bankToGhostDir * off, bankToGhostDir, bankToGhostDist - off, blockerPuck.gameObject, gameObject);
                            bool r2R = CheckRayPucksOnly(bankPoint - bankToGhostPerp * strikerRadius + bankToGhostDir * off, bankToGhostDir, bankToGhostDist - off, blockerPuck.gameObject, gameObject);
                            Debug.Log($"[LEFT-WALL] r2 C={r2C} L={r2L} R={r2R}");
                            if (r2C || r2L || r2R) continue;

                            Vector2 b2pDir = (blockerPos - bankPoint).normalized;
                            float b2pDist = Vector2.Distance(bankPoint, blockerPos);
                            Vector2 b2pPerp = new Vector2(-b2pDir.y, b2pDir.x);
                            bool holeC = CheckRayForHoles(bankPoint + b2pDir * off, b2pDir, b2pDist - off);
                            bool holeL = CheckRayForHoles(bankPoint + b2pPerp * strikerRadius + b2pDir * off, b2pDir, b2pDist - off);
                            bool holeR = CheckRayForHoles(bankPoint - b2pPerp * strikerRadius + b2pDir * off, b2pDir, b2pDist - off);
                            Debug.Log($"[LEFT-WALL] holes C={holeC} L={holeL} R={holeR}");
                            if (holeC || holeL || holeR) continue;

                            Debug.Log("[LEFT-WALL] ALL CHECKS PASSED — should register!");

                            ShotLines lines = BuildCannonLines(testPos, blockerPos, targetPos, strikerRadius, blockerR, b2tDir);
                            Debug.Log($"[BANK CANNON CANDIDATE] wall=IsVertical:{wall.IsVertical} wallPos={wall.Position:F2}  alignAngle={alignAngle:F2}  currentBest={result.Score:F2}  blocker={blockerPuck.name}  target={targetPuck.name}  bankPt={bankPoint}");
                            if (result.TryUpdate(alignAngle, testPos, toBankDir, blockerPos, blockerR,
                                                targetPos, holePos, hole.name,
                                                blockerPuck.gameObject, targetPuck.gameObject, lines))
                            {
                                Debug.Log($"[BotAI][BANK CANNON] BankPt={bankPoint}  Blocker={blockerPuck.name}  Target={targetPuck.name}  Wall=IsVertical:{wall.IsVertical}");
                            }
                        }
                    }
                }
            }
        }

        // ── Hole eligibility for a given black puck / striker position ─────────────

        private bool IsHoleEligibleForBlackPuck(
            Vector2 testPos, Vector2 puckPos, Vector2 holePos,
            Transform hole, HoleLayout layout, float strikerRadius,
            BlackScanState state, Puck puck)
        {
            // Low pucks must target bottom holes only
            if (puckPos.y < 1f && hole != layout.BottomLeft && hole != layout.BottomRight)
                return false;

            // Area restrictions
            string area = puck.whichArea;
            if (area == GameController.BOTTOM_RIGHT_SECTION && hole.name != GameController.BOTTOM_RIGHT_HOLE) return false;
            if (area == GameController.BOTTOM_LEFT_SECTION && hole.name != GameController.BOTTOM_LEFT_HOLE) return false;
            // (puck.whichArea is checked by the caller before entering this method)

            if (puckPos.y >= 0.815f && puckPos.y < 1.15f && Mathf.Abs(puckPos.x) > 1.5f)
            {
                if (puckPos.x < 0f)
                    return hole == layout.BottomLeft;
                else
                    return hole == layout.BottomRight;
            }

            // Bottom-corner directional constraint
            if (hole.name == GameController.BOTTOM_LEFT_HOLE && puckPos.x >= testPos.x) return false;
            if (hole.name == GameController.BOTTOM_RIGHT_HOLE && puckPos.x <= testPos.x) return false;

            // High pucks → top holes only, striker on opposite side
            if (puckPos.y >= 1.15f)
            {
                bool isTopHole = (hole == layout.TopLeft || hole == layout.TopRight);
                if (!isTopHole) return false;

                float dL = layout.TopLeft != null ? Vector2.Distance(puckPos, layout.TopLeft.position) : float.MaxValue;
                float dR = layout.TopRight != null ? Vector2.Distance(puckPos, layout.TopRight.position) : float.MaxValue;
                Transform nearestTop = dL <= dR ? layout.TopLeft : layout.TopRight;
                Transform otherTop = dL <= dR ? layout.TopRight : layout.TopLeft;

                if (hole == otherTop && nearestTop != null)
                {
                    Vector2 nearestPos = (Vector2)nearestTop.position;  // ← explicit cast
                    Vector2 nearestDir = (nearestPos - puckPos).normalized;
                    float nearestDist = Vector2.Distance(puckPos, nearestPos);
                    float off = strikerRadius + 0.05f;
                    if (!CheckRay(puckPos + nearestDir * off, nearestDir, nearestDist - off, null, gameObject))
                        return false;
                }

                float distToHole = Vector2.Distance(puckPos, holePos);
                if (distToHole < 0.5f) return true;

                bool topHoleIsLeft = holePos.x <= layout.MidX;
                return topHoleIsLeft ? testPos.x > layout.MidX : testPos.x <= layout.MidX;
            }

            // Mid-height pucks targeting top holes
            if (hole == layout.TopLeft || hole == layout.TopRight)
            {
                float dL = Vector2.Distance(puckPos, layout.TopLeft.position);
                float dR = Vector2.Distance(puckPos, layout.TopRight.position);
                Transform nearestTop = dL <= dR ? layout.TopLeft : layout.TopRight;
                if (hole != nearestTop) return false;

                bool topHoleIsLeft = holePos.x <= layout.MidX;
                if (!(topHoleIsLeft ? testPos.x > layout.MidX : testPos.x <= layout.MidX)) return false;

                // Only extreme striker position allowed for this special case
                const float step = 0.05f;
                float requiredX = topHoleIsLeft ? GameController.Instance.playerPuckMaxX : GameController.Instance.playerPuckMinX;
                if (Mathf.Abs(testPos.x - requiredX) > step * 0.5f) return false;

                state.FoundUsableTopFallbackCandidate = true;
            }

            return true;
        }

        private bool IsNearZeroTopOppositeQualified(
            Vector2 testPos, Vector2 puckPos, Vector2 holePos,
            Transform hole, float dist1, HoleLayout layout, bool isNearZeroAny)
        {
            if (!isNearZeroAny) return false;
            bool holeIsLeft = holePos.x <= layout.MidX;
            Transform topHole = holeIsLeft ? layout.TopLeft : layout.TopRight;
            float line12 = holeIsLeft ? layout.Line12Left : layout.Line12Right;
            if (topHole == null || hole != topHole || dist1 > line12 * 0.2f) return false;
            return holeIsLeft ? testPos.x > layout.MidX : testPos.x <= layout.MidX;
        }

        private bool IsNearZeroBottomSameQualified(
            Vector2 testPos, Vector2 holePos, Transform hole, HoleLayout layout, bool isNearZeroAny)
        {
            if (!isNearZeroAny) return false;
            bool holeIsLeft = holePos.x <= layout.MidX;
            Transform bottomHole = holeIsLeft ? layout.BottomLeft : layout.BottomRight;
            if (bottomHole == null || hole != bottomHole) return false;
            return holeIsLeft ? testPos.x <= layout.MidX : testPos.x > layout.MidX;
        }

        private bool ShouldAllowFallback(Vector2 puckPos, Transform hole, HoleLayout layout, bool foundTopFallback)
        {
            if (puckPos.y >= 1.15f) return true;
            if (hole == layout.TopLeft || hole == layout.TopRight) return foundTopFallback;
            if (hole == layout.BottomLeft || hole == layout.BottomRight) return !foundTopFallback;
            return true;
        }

        private Vector2 ComputeBlackShootDir(
            Vector2 testPos, Vector2 puckPos, Vector2 holePos,
            Vector2 line1Dir, Vector2 line2Dir,
            float combinedR, float angle4,
            bool nearZeroTopOpp, bool nearZeroBottomSame, bool forcedTopFallback)
        {
            // ── REMOVE the special nearZeroTopOpp branch entirely ──
            // The old code aimed at puckPos - line1Dir*(combinedR*0.95f), which
            // ignores angle4 and shoots almost straight through the puck center.

            if (nearZeroBottomSame)
            {
                Vector2 ghostBall = puckPos - line1Dir * combinedR;
                return (ghostBall - testPos).normalized;
            }
            if (forcedTopFallback)
                return (puckPos - testPos).normalized;

            // Standard ghost-ball with cross-product rotation — used for ALL cases now,
            // including nearZeroTopOpp. angle4 encodes the correct cut angle.
            float cross = line2Dir.x * line1Dir.y - line2Dir.y * line1Dir.x;
            float signedA4 = angle4 * -Mathf.Sign(cross);
            float rad = signedA4 * Mathf.Deg2Rad;
            float cos4 = Mathf.Cos(rad), sin4 = Mathf.Sin(rad);
            return new Vector2(
                line2Dir.x * cos4 - line2Dir.y * sin4,
                line2Dir.x * sin4 + line2Dir.y * cos4).normalized;
        }

        private void RecordBestBlackShot(
            Vector2 testPos, Vector2 puckPos, Vector2 holePos, Transform hole, Vector2 shootDir,
            float angle2, float angle4, float angle1Up, float angle1Down, float scaleFactor, float angle3,
            bool isNearZeroUp, bool isNearZeroDown,
            bool nearZeroTopOpp, bool nearZeroBottomSame, bool forcedTopFallback,
            ShotLines lines, HoleLayout layout, BlackScanState state)
        {
            void UpdateDebug()
            {
                state.A1Up = angle1Up; state.A1Down = angle1Down;
                state.A2 = angle2; state.A3 = angle3;
                state.Scale = scaleFactor;
                state.IsNearZeroUp = isNearZeroUp; state.IsNearZeroDown = isNearZeroDown;
            }

            if (nearZeroTopOpp)
            {
                if (state.NearZeroTopOpposite.TryUpdate(angle4, testPos, shootDir, puckPos, holePos, hole.name, lines))
                {
                    state.HasAnyNearZero = true;
                    if (angle2 < state.NearZeroA2Min) state.NearZeroA2Min = angle2;
                    if (angle2 > state.NearZeroA2Max) state.NearZeroA2Max = angle2;
                    UpdateDebug();
                }
                return;
            }

            if (nearZeroBottomSame)
            {
                bool holeIsLeft = holePos.x <= layout.MidX;
                float sideScore = holeIsLeft ? testPos.x : -testPos.x;
                if (state.NearZeroBottomSame.TryUpdate(sideScore, testPos, shootDir, puckPos, holePos, hole.name, lines))
                    UpdateDebug();
                return;
            }

            if (forcedTopFallback) return; // handled by Fallback only

            if (puckPos.y >= 1.15f)
            {
                Debug.Log($"CBC TestPost {testPos.x}");
                bool topHoleIsLeft = holePos.x <= layout.MidX;
                // Hole is left → disc must be on right → go to far right extreme
                // Hole is right → disc must be on left → go to far left extreme
                float forcedX = topHoleIsLeft
                    ? GameController.Instance.playerPuckMaxX
                    : GameController.Instance.playerPuckMinX;

                float cornerDist = Mathf.Abs(testPos.x - forcedX);
                if (state.HighPuckForced.TryUpdate(cornerDist, testPos, shootDir, puckPos, holePos, hole.name, lines))
                    UpdateDebug();
                return;
            }

            if (state.Normal.TryUpdate(angle4, testPos, shootDir, puckPos, holePos, hole.name, lines))
                UpdateDebug();
        }

        private (bool redOnBoard, bool mustTryRed, bool shouldTryRed, float redPuckRadius)
            EvaluateRedPuckStatus(Puck[] blackPucks)
        {
            GameObject redGO = GameController.Instance.redPuck;
            bool redOnBoard = redGO != null
                            && redGO.activeSelf
                            && !GameController.Instance.redPuckCollected
                            && !redGO.GetComponent<Puck>().isPocketed;

            bool mustTryRed = blackPucks.Length == 1 && redOnBoard;
            bool shouldTryRed = (GameController.Instance.awayPucksCollected.Count >= 7 || mustTryRed) && redOnBoard;

            float redPuckRadius = 0.1f;
            if (redOnBoard)
            {
                var cc = redGO.GetComponent<CircleCollider2D>();
                if (cc != null) redPuckRadius = cc.radius * Mathf.Abs(redGO.transform.localScale.x);
            }

            Debug.Log($"MustTryRed={mustTryRed}  ShouldTryRed={shouldTryRed}");
            return (redOnBoard, mustTryRed, shouldTryRed, redPuckRadius);
        }

        private void ScanRedPuckAtPosition(
            Vector2 testPos, HoleLayout layout,
            float strikerRadius, float redPuckRadius, float nearZeroThreshold,
            RedScanState state)
        {
            GameObject redGO = GameController.Instance.redPuck;
            Vector2 redPos = redGO.transform.position;
            float combinedR = redPuckRadius + strikerRadius;

            for (int hi = 0; hi < layout.All.Length; hi++)
            {
                Transform hole = layout.All[hi];
                Vector2 holePos = hole.position;

                if (!IsHoleEligibleForRedPuck(testPos, redPos, holePos, hole, redGO, layout, strikerRadius))
                    continue;

                Vector2 line1 = holePos - redPos;
                float dist1 = line1.magnitude;
                Vector2 line1Dir = line1.normalized;
                Vector2 line2 = redPos - testPos;
                float dist2 = line2.magnitude;
                Vector2 line2Dir = line2.normalized;

                if (dist1 >= Vector2.Distance(testPos, holePos)) continue;

                Vector2 perp1 = new Vector2(-line1Dir.y, line1Dir.x);
                Vector2 perp2 = new Vector2(-line2Dir.y, line2Dir.x);

                ShotLines lines = ShotLines.Make(
                    redPos + perp1 * redPuckRadius, holePos + perp1 * redPuckRadius,
                    redPos - perp1 * redPuckRadius, holePos - perp1 * redPuckRadius,
                    testPos + perp2 * strikerRadius, redPos + perp2 * strikerRadius,
                    testPos - perp2 * strikerRadius, redPos - perp2 * strikerRadius);

                float angle1Up = Vector2.Angle(Vector2.up, line1);
                float angle1Down = Vector2.Angle(Vector2.down, line1);
                bool isNearZeroUp = angle1Up < nearZeroThreshold;
                bool isNearZeroDown = angle1Down < nearZeroThreshold;
                bool isNearZeroAny = isNearZeroUp || isNearZeroDown;

                float angle2 = Mathf.Atan2(combinedR, dist2) * Mathf.Rad2Deg;
                if (angle2 < 0.001f) continue;

                float scaleFactor = angle1Up > 0.001f ? angle2 / angle1Up : 0f;
                float angle3 = Vector2.Angle(line1, line2);
                float angle4 = Mathf.Clamp(angle3 * scaleFactor, 0f, angle2);

                bool nearZeroTopOpp = IsNearZeroTopOppositeQualified(testPos, redPos, holePos, hole, dist1, layout, isNearZeroAny);

                Vector2 shootDir;
                if (nearZeroTopOpp)
                {
                    Vector2 ghostBall = redPos - line1Dir * (combinedR * 0.95f);
                    shootDir = (ghostBall - testPos).normalized;
                }
                else
                {
                    float cross = line2Dir.x * line1Dir.y - line2Dir.y * line1Dir.x;
                    float signedA4 = angle4 * -Mathf.Sign(cross);
                    float rad = signedA4 * Mathf.Deg2Rad;
                    shootDir = new Vector2(
                        line2Dir.x * Mathf.Cos(rad) - line2Dir.y * Mathf.Sin(rad),
                        line2Dir.x * Mathf.Sin(rad) + line2Dir.y * Mathf.Cos(rad)).normalized;
                }

                float startOffset = strikerRadius + 0.05f;
                Vector2 perp2Shoot = new Vector2(-shootDir.y, shootDir.x);

                bool centerBlocked1 = CheckRay(redPos + line1Dir * startOffset, line1Dir,
                    dist1 - startOffset, redGO, gameObject);
                bool centerBlocked2 = CheckRay(testPos + shootDir * startOffset, shootDir,
                    dist2 - startOffset, redGO, gameObject);
                bool l1LBlocked = CheckRay(lines.L1LS + line1Dir * startOffset, line1Dir,
                    dist1 - startOffset, redGO, gameObject);
                bool l1RBlocked = CheckRay(lines.L1RS + line1Dir * startOffset, line1Dir,
                    dist1 - startOffset, redGO, gameObject);
                bool l2LBlocked = CheckRay(testPos + perp2Shoot * strikerRadius + shootDir * startOffset, shootDir,
                    dist2 - startOffset, redGO, gameObject);
                bool l2RBlocked = CheckRay(testPos - perp2Shoot * strikerRadius + shootDir * startOffset, shootDir,
                    dist2 - startOffset, redGO, gameObject);

                // Always record a fallback (ignoring line2 obstacles)
                state.Fallback.TryUpdate(angle4, testPos, shootDir, redPos, holePos, hole.name, lines);

                if (centerBlocked1 || centerBlocked2 || l1LBlocked || l1RBlocked || l2LBlocked || l2RBlocked) continue;

                if (state.DirectShot.TryUpdate(angle4, testPos, shootDir, redPos, holePos, hole.name, lines))
                {
                    state.IsNearZeroUp = isNearZeroUp;
                    state.IsNearZeroDown = isNearZeroDown;
                    state.IsNearZeroTopOpposite = nearZeroTopOpp;
                }
            }
        }

        private void ScanBankShotsRedAtPosition(
            Vector2 testPos, HoleLayout layout,
            float strikerRadius, float redPuckRadius, WallInfo[] walls, RedScanState state)
        {
            GameObject redGO = GameController.Instance.redPuck;
            Vector2 redPos = redGO.transform.position;
            float combinedR = redPuckRadius + strikerRadius;
            float off = strikerRadius + 0.05f;

            for (int hi = 0; hi < layout.All.Length; hi++)
            {
                Transform hole = layout.All[hi];
                Vector2 holePos = hole.position;

                Vector2 line1 = holePos - redPos;
                float dist1 = line1.magnitude;
                Vector2 line1Dir = line1.normalized;

                bool centerBlocked1 = CheckRayPucksOnly(redPos + line1Dir * off, line1Dir, dist1 - off, redGO, gameObject);
                Vector2 perp1 = new Vector2(-line1Dir.y, line1Dir.x);
                bool l1LBlocked = CheckRayPucksOnly(redPos + perp1 * redPuckRadius + line1Dir * off, line1Dir, dist1 - off, redGO, gameObject);
                bool l1RBlocked = CheckRayPucksOnly(redPos - perp1 * redPuckRadius + line1Dir * off, line1Dir, dist1 - off, redGO, gameObject);
                if (centerBlocked1 || l1LBlocked || l1RBlocked) continue;

                Vector2 line2 = redPos - testPos;
                float dist2 = line2.magnitude;
                float angle1Up = Vector2.Angle(Vector2.up, line1);
                float angle2 = Mathf.Atan2(combinedR, dist2) * Mathf.Rad2Deg;
                if (angle2 < 0.001f) continue;
                float scaleFactor = angle1Up > 0.001f ? angle2 / angle1Up : 0f;
                float angle3 = Vector2.Angle(line1, line2);
                float angle4 = Mathf.Clamp(angle3 * scaleFactor, 0f, angle2);

                foreach (WallInfo wall in walls)
                {
                    // ── Hole restrictions per wall type ──────────────────────────
                    if (!wall.IsVertical)
                    {
                        if (hole != layout.TopLeft && hole != layout.TopRight) continue;
                        if (testPos.x > redPos.x && hole != layout.TopLeft) continue;
                        if (testPos.x < redPos.x && hole != layout.TopRight) continue;
                    }

                    // ── Compute bank point ────────────────────────────────────────
                    Vector2 bankPoint;
                    if (!wall.IsVertical)
                    {
                        Vector2 mirroredPos = new Vector2(testPos.x, 2f * wall.Position - testPos.y);
                        Vector2 dir2Bank = redPos - mirroredPos;
                        if (Mathf.Abs(dir2Bank.y) < 0.001f) continue;
                        float t = (wall.Position - mirroredPos.y) / dir2Bank.y;
                        if (t <= 0f || t >= 1f) continue;
                        bankPoint = mirroredPos + dir2Bank * t;
                        if (bankPoint.x < wall.Min || bankPoint.x > wall.Max) continue;
                        if (bankPoint.x < -1.8f || bankPoint.x > 1.8f) continue;
                        float wallCenterX = (wall.Min + wall.Max) * 0.5f;
                        bankPoint = new Vector2(Mathf.Lerp(wallCenterX, bankPoint.x, bankAngleBias), bankPoint.y);
                        if (testPos.y <= wall.Position) continue;
                        if (Mathf.Abs(testPos.x - redPos.x) < 0.5f) continue;
                    }
                    else
                    {
                        Vector2 mirroredPos = new Vector2(2f * wall.Position - testPos.x, testPos.y);
                        Vector2 dir2Bank = redPos - mirroredPos;
                        if (Mathf.Abs(dir2Bank.x) < 0.001f) continue;
                        float t = (wall.Position - mirroredPos.x) / dir2Bank.x;
                        if (t <= 0f || t >= 1f) continue;
                        bankPoint = mirroredPos + dir2Bank * t;
                        if (bankPoint.y < wall.Min || bankPoint.y > wall.Max) continue;
                        if (bankPoint.y < -2.0f || bankPoint.y > 2.0f) continue;
                        float wallCenterY = (wall.Min + wall.Max) * 0.5f;
                        bankPoint = new Vector2(bankPoint.x, Mathf.Lerp(wallCenterY, bankPoint.y, bankAngleBias));
                        if ((testPos.x - wall.Position) * (redPos.x - wall.Position) <= 0) continue;
                        if (Mathf.Abs(testPos.y - redPos.y) < 0.5f) continue;
                    }

                    Vector2 toBankDir = (bankPoint - testPos).normalized;
                    float toBankDist = Vector2.Distance(testPos, bankPoint);
                    Vector2 bankPerp = new Vector2(-toBankDir.y, toBankDir.x);

                    bool ray1Center = CheckRayPucksOnly(testPos + toBankDir * off, toBankDir, toBankDist - off, redGO, gameObject);
                    bool ray1Left = CheckRayPucksOnly(testPos + bankPerp * strikerRadius + toBankDir * off, toBankDir, toBankDist - off, redGO, gameObject);
                    bool ray1Right = CheckRayPucksOnly(testPos - bankPerp * strikerRadius + toBankDir * off, toBankDir, toBankDist - off, redGO, gameObject);
                    if (ray1Center || ray1Left || ray1Right) continue;

                    Vector2 bankToPuckDir = (redPos - bankPoint).normalized;
                    float bankToPuckDist = Vector2.Distance(bankPoint, redPos);
                    Vector2 bankToPuckPerp = new Vector2(-bankToPuckDir.y, bankToPuckDir.x);

                    bool ray2Center = CheckRayPucksOnly(bankPoint + bankToPuckDir * off, bankToPuckDir, bankToPuckDist - off, redGO, gameObject);
                    bool ray2Left = CheckRayPucksOnly(bankPoint + bankToPuckPerp * strikerRadius + bankToPuckDir * off, bankToPuckDir, bankToPuckDist - off, redGO, gameObject);
                    bool ray2Right = CheckRayPucksOnly(bankPoint - bankToPuckPerp * strikerRadius + bankToPuckDir * off, bankToPuckDir, bankToPuckDist - off, redGO, gameObject);
                    if (ray2Center || ray2Left || ray2Right) continue;

                    Vector2 perp2 = new Vector2(-toBankDir.y, toBankDir.x);
                    ShotLines lines = ShotLines.Make(
                        redPos + perp1 * redPuckRadius, holePos + perp1 * redPuckRadius,
                        redPos - perp1 * redPuckRadius, holePos - perp1 * redPuckRadius,
                        testPos + perp2 * strikerRadius, bankPoint + perp2 * strikerRadius,
                        testPos - perp2 * strikerRadius, bankPoint - perp2 * strikerRadius);

                    if (state.BankShot.TryUpdate(angle4, testPos, toBankDir, redPos, holePos, hole.name, lines))
                        Debug.Log($"[BotAI][RED BANK] BankPt={bankPoint}  A4={angle4:F2}°  Wall=IsVertical:{wall.IsVertical}");
                }
            }
        }

        private bool IsHoleEligibleForRedPuck(
            Vector2 testPos, Vector2 redPos, Vector2 holePos,
            Transform hole, GameObject redGO, HoleLayout layout, float strikerRadius)
        {
            string redArea = redGO.GetComponent<Puck>().whichArea;
            if (redArea == GameController.BOTTOM_RIGHT_SECTION && hole.name != GameController.BOTTOM_RIGHT_HOLE) return false;
            if (redArea == GameController.BOTTOM_LEFT_SECTION && hole.name != GameController.BOTTOM_LEFT_HOLE) return false;

            if (hole.name == GameController.BOTTOM_LEFT_HOLE && redPos.x >= testPos.x) return false;
            if (hole.name == GameController.BOTTOM_RIGHT_HOLE && redPos.x <= testPos.x) return false;

            if (redPos.y >= 1.15f)
            {
                bool isTopHole = (hole == layout.TopLeft || hole == layout.TopRight);
                if (!isTopHole) return false;

                float dL = layout.TopLeft != null ? Vector2.Distance(redPos, layout.TopLeft.position) : float.MaxValue;
                float dR = layout.TopRight != null ? Vector2.Distance(redPos, layout.TopRight.position) : float.MaxValue;
                Transform nearestTop = dL <= dR ? layout.TopLeft : layout.TopRight;
                Transform otherTop = dL <= dR ? layout.TopRight : layout.TopLeft;

                if (hole == otherTop && nearestTop != null)
                {
                    Vector2 nearestPos = (Vector2)nearestTop.position;  // ← explicit cast
                    Vector2 nearDir = (nearestPos - redPos).normalized;
                    float nearDist = Vector2.Distance(redPos, nearestPos);
                    float off = strikerRadius + 0.05f;
                    if (!CheckRay(redPos + nearDir * off, nearDir, nearDist - off, redGO, gameObject))
                        return false;
                }

                bool topIsLeft = holePos.x <= layout.MidX;
                return topIsLeft ? testPos.x > layout.MidX : testPos.x <= layout.MidX;
            }

            return true;
        }

        private void ScanCannonBlackAtPosition(
            Vector2 testPos, Puck[] blackPucks, HoleLayout layout,
            float strikerRadius, CannonCandidate result)
        {
            foreach (Puck targetPuck in blackPucks)
            {
                Vector2 targetPos = targetPuck.transform.position;
                float targetR = targetPuck.GetComponent<CircleCollider2D>().radius * Mathf.Abs(targetPuck.transform.localScale.x);

                for (int hi = 0; hi < layout.All.Length; hi++)
                {
                    Transform hole = layout.All[hi];
                    Vector2 holePos = hole.position;

                    if (targetPos.y >= 1.15f && hole != layout.TopLeft && hole != layout.TopRight) continue;

                    if (!IsLine1ClearForCannon(targetPos, targetR, holePos, strikerRadius, targetPuck.gameObject)) continue;

                    // Find the closest blocker between testPos and targetPos
                    if (!TryFindBlocker(testPos, targetPos, strikerRadius, targetPuck.gameObject,
                                        out GameObject blockerGO, out Vector2 blockerPos, out float blockerR)) continue;

                    if (targetPos.y < 1.15f && blockerPos.y < targetPos.y) continue;
                    if (hole == layout.BottomLeft && targetPos.x > blockerPos.x) continue;
                    if (hole == layout.BottomRight && targetPos.x < blockerPos.x) continue;

                    if (hole == layout.TopLeft && targetPos.x > blockerPos.x) continue;
                    if (hole == layout.TopRight && targetPos.x < blockerPos.x) continue;

                    Vector2 toHole = (holePos - targetPos).normalized;
                    Vector2 b2tDir = (targetPos - blockerPos).normalized;
                    float alignAngle = Vector2.Angle(b2tDir, toHole);
                    if (alignAngle > 50f) continue;

                    if (hole == layout.BottomLeft && testPos.x < layout.MidX) continue;
                    if (hole == layout.BottomRight && testPos.x > layout.MidX) continue;

                    if (hole == layout.BottomLeft && testPos.x < blockerPos.x - 0.5f) continue;
                    if (hole == layout.BottomRight && testPos.x > blockerPos.x + 0.5f) continue;

                    if (hole == layout.TopLeft && testPos.x < blockerPos.x + .5f) continue;
                    if (hole == layout.TopRight && testPos.x > blockerPos.x - .5f) continue;

                    if (!IsLine1ClearForCannon(blockerPos, blockerR, targetPos, strikerRadius, targetPuck.gameObject)) continue;

                    if (!IsLine1ClearForCannon(testPos, strikerRadius, blockerPos, strikerRadius, blockerGO)) continue;

                    float combinedRS = strikerRadius + blockerR;
                    Vector2 ghostBall = blockerPos - b2tDir * combinedRS;
                    Vector2 cannonDir = (ghostBall - testPos).normalized;
                    ShotLines lines = BuildCannonLines(testPos, blockerPos, targetPos, strikerRadius, blockerR, b2tDir);

                    if (result.TryUpdate(alignAngle, testPos, cannonDir, blockerPos, blockerR,
                                        targetPos, holePos, hole.name,
                                        blockerGO, targetPuck.gameObject, lines))
                    {
                        Debug.Log($"[BotAI][CANNON] Blocker={blockerGO.name}  Target={targetPuck.name}  AlignAngle={alignAngle:F2}°");
                    }
                }
            }
        }

        private void ScanCannonRedAtPosition(
            Vector2 testPos, HoleLayout layout,
            float strikerRadius, float redPuckRadius, CannonCandidate result)
        {
            GameObject redGO = GameController.Instance.redPuck;
            Vector2 redPos = redGO.transform.position;

            for (int hi = 0; hi < layout.All.Length; hi++)
            {
                Transform hole = layout.All[hi];
                Vector2 holePos = hole.position;

                if (redPos.y >= 1.1f && hole != layout.TopLeft && hole != layout.TopRight) continue;
                if (!IsLine1ClearForCannon(redPos, redPuckRadius, holePos, strikerRadius, redGO)) continue;

                if (!TryFindBlocker(testPos, redPos, strikerRadius, redGO,
                                    out GameObject blockerGO, out Vector2 blockerPos, out float blockerR)) continue;

                if (hole == layout.BottomLeft && redPos.x > blockerPos.x) continue;
                if (hole == layout.BottomRight && redPos.x < blockerPos.x) continue;
                if (redPos.y < 1.15f && blockerPos.y < redPos.y) continue;

                if (hole == layout.TopLeft && redPos.x > blockerPos.x) continue;
                if (hole == layout.TopRight && redPos.x < blockerPos.x) continue;

                Vector2 toHole = (holePos - redPos).normalized;
                Vector2 b2rDir = (redPos - blockerPos).normalized;
                float alignAngle = Vector2.Angle(b2rDir, toHole);
                if (alignAngle > 40f) continue;

                if (hole == layout.BottomLeft && testPos.x < layout.MidX) continue;
                if (hole == layout.BottomRight && testPos.x > layout.MidX) continue;

                if (hole == layout.BottomLeft && testPos.x < blockerPos.x - 0.5f) continue;
                if (hole == layout.BottomRight && testPos.x > blockerPos.x + 0.5f) continue;

                if (hole == layout.TopLeft && testPos.x < blockerPos.x + .5f) continue;
                if (hole == layout.TopRight && testPos.x > blockerPos.x - .5f) continue;

                if (!IsLine1ClearForCannon(blockerPos, blockerR, redPos, strikerRadius, redGO)) continue;

                if (!IsLine1ClearForCannon(testPos, strikerRadius, blockerPos, strikerRadius, blockerGO)) continue;

                float combinedRS = strikerRadius + blockerR;
                Vector2 ghostBall = blockerPos - b2rDir * combinedRS;
                Vector2 cannonDir = (ghostBall - testPos).normalized;
                ShotLines lines = BuildCannonLines(testPos, blockerPos, redPos, strikerRadius, blockerR, b2rDir);

                if (result.TryUpdate(alignAngle, testPos, cannonDir, blockerPos, blockerR,
                                    redPos, holePos, hole.name,
                                    blockerGO, redGO, lines))
                {
                    Debug.Log($"[BotAI][RED CANNON] Blocker={blockerGO.name}  RedPos={redPos}  AlignAngle={alignAngle:F2}°");
                }
            }
        }

        private bool IsLine1ClearForCannon(Vector2 from, float fromR, Vector2 to, float strikerRadius, GameObject excludeGO)
        {
            Vector2 dir = (to - from).normalized;
            float dist = Vector2.Distance(from, to);
            float off = strikerRadius + 0.05f;

            Vector2 perp = new Vector2(-dir.y, dir.x);
            return !CheckRay(from + dir * off, dir, dist - off, excludeGO, gameObject) &&
                !CheckRay(from + perp * fromR + dir * off, dir, dist - off, excludeGO, gameObject) &&
                !CheckRay(from - perp * fromR + dir * off, dir, dist - off, excludeGO, gameObject);
        }

        private bool TryFindBlocker(
            Vector2 from, Vector2 to, float strikerRadius, GameObject excludeGO,
            out GameObject blockerGO, out Vector2 blockerPos, out float blockerR)
        {
            blockerGO = null; blockerPos = default; blockerR = 0f;
            float closestD = float.MaxValue;
            Vector2 dir = (to - from).normalized;
            float dist = Vector2.Distance(from, to);
            float off = strikerRadius + 0.05f;
            Vector2 perp = new Vector2(-dir.y, dir.x);

            Vector2[] origins = { from, from + perp * strikerRadius, from - perp * strikerRadius };
            foreach (Vector2 origin in origins)
            {
                foreach (RaycastHit2D hit in Physics2D.RaycastAll(origin + dir * off, dir, dist - off))
                {
                    GameObject go = hit.collider.gameObject;
                    if (go == excludeGO || go == gameObject) continue;
                    if (hit.collider.CompareTag("Hole")) continue;
                    string tag = go.tag;
                    if (tag != "Black" && tag != "White") continue;

                    if (hit.distance < closestD)
                    {
                        closestD = hit.distance;
                        blockerGO = go;
                        blockerPos = go.transform.position;
                        var cc = go.GetComponent<CircleCollider2D>();
                        blockerR = cc != null ? cc.radius * Mathf.Abs(go.transform.localScale.x) : 0.1f;
                    }
                }
            }

            return blockerGO != null;
        }

        private ShotLines BuildCannonLines(
            Vector2 strikerPos, Vector2 blockerPos, Vector2 targetPos,
            float strikerRadius, float blockerR, Vector2 b2tDir)
        {
            Vector2 perp1 = new Vector2(-b2tDir.y, b2tDir.x);
            Vector2 l2Dir = (blockerPos - strikerPos).normalized;
            Vector2 perp2 = new Vector2(-l2Dir.y, l2Dir.x);

            return ShotLines.Make(
                blockerPos + perp1 * blockerR, targetPos + perp1 * blockerR,
                blockerPos - perp1 * blockerR, targetPos - perp1 * blockerR,
                strikerPos + perp2 * strikerRadius, blockerPos + perp2 * strikerRadius,
                strikerPos - perp2 * strikerRadius, blockerPos - perp2 * strikerRadius);
        }

        private FinalShotInfo SelectFinalShot(
            BlackScanState blackState, RedScanState redState,
            bool mustTryRed, bool shouldTryRed)
        {
            FinalShotInfo FromCandidate(ShotCandidate c, string type)
            {
                var fi = new FinalShotInfo { Type = type };
                fi.StrikerPos = c.StrikerPos; fi.ShootDir = c.ShootDir;
                fi.PuckPos = c.PuckPos; fi.HolePos = c.HolePos;
                fi.HoleName = c.HoleName; fi.Lines = c.Lines;
                fi.IsNearZeroUp = c.IsNearZeroUp;
                fi.IsNearZeroDown = c.IsNearZeroDown;
                fi.IsNearZeroTopOpposite = c.IsNearZeroTopOpposite;
                return fi;
            }

            FinalShotInfo FromCannon(CannonCandidate c, string type, bool isRed)
            {
                var fi = new FinalShotInfo { Type = type };
                fi.StrikerPos = c.StrikerPos; fi.ShootDir = c.ShootDir;
                fi.HolePos = c.HolePos; fi.HoleName = c.HoleName;
                fi.PuckPos = c.BlockerPos;  // "PuckPos" holds the blocker for line-draw purposes
                fi.Lines = c.Lines;
                fi.CannonBlockerPos = c.BlockerPos; fi.CannonBlockerR = c.BlockerR;
                fi.CannonTargetPos = c.TargetPos;
                fi.CannonBlockerGO = c.BlockerGO; fi.CannonTargetGO = c.TargetGO;
                return fi;
            }

            // Priority order matches original code
            if (mustTryRed && redState.DirectShot.Valid)
                return FromCandidate(redState.DirectShot, "RED PUCK SHOT (MANDATORY — last black)");

            if (mustTryRed && redState.Cannon.Valid)
            {
                Debug.Log("[BotAI] Mandatory red cannon shot selected.");
                return FromCannon(redState.Cannon, "RED CANNON SHOT (MANDATORY)", true);
            }

            if (mustTryRed && redState.Fallback.Valid)
            {
                Debug.LogWarning("[BotAI] mustTryRed — no clear red shot, shooting through obstacle.");
                return FromCandidate(redState.Fallback, "RED PUCK SHOT (MANDATORY — obstacle ignored)");
            }

            if (blackState.NearZeroTopOpposite.Valid)
                return FromCandidate(blackState.NearZeroTopOpposite, "NEAR-ZERO TOP-HOLE (OPPOSITE SIDE)");

            if (blackState.NearZeroBottomSame.Valid)
                return FromCandidate(blackState.NearZeroBottomSame, "NEAR-ZERO BOTTOM-HOLE (SAME SIDE)");

            if (blackState.HighPuckForced.Valid)
                return FromCandidate(blackState.HighPuckForced, "HIGH PUCK FORCED CORNER");

            if (shouldTryRed && redState.DirectShot.Valid)
            {
                var fi = FromCandidate(redState.DirectShot, "RED PUCK SHOT");
                fi.IsNearZeroUp = redState.IsNearZeroUp;
                fi.IsNearZeroDown = redState.IsNearZeroDown;
                fi.IsNearZeroTopOpposite = redState.IsNearZeroTopOpposite;
                return fi;
            }

            if (shouldTryRed && redState.Cannon.Valid)
            {
                Debug.Log($"[BotAI] Opportunistic red cannon shot. AlignAngle={redState.Cannon.Score:F2}°");
                return FromCannon(redState.Cannon, "RED CANNON SHOT", true);
            }

            if (shouldTryRed && redState.BankShot.Valid)
            {
                return FromCandidate(redState.BankShot, "RED BANK SHOT");
            }

            if (blackState.Normal.Valid)
                return FromCandidate(blackState.Normal, "NORMAL FULL");

            if (blackState.Cannon.Valid)
            {
                Debug.Log($"[BotAI] Cannon shot selected. AlignAngle={blackState.Cannon.Score:F2}°");
                return FromCannon(blackState.Cannon, "CANNON SHOT", false);
            }

            if (blackState.BankCannon.Valid)
            {
                Debug.Log($"[BotAI] Bank cannon shot selected. AlignAngle={blackState.BankCannon.Score:F2}°");
                return FromCannon(blackState.BankCannon, "BANK CANNON SHOT", false);
            }

            // ── Bank shot — after cannon, before fallback ─────────────────────
            if (blackState.BankShot.Valid)
            {
                return FromCandidate(blackState.BankShot, "BANK SHOT");
            }

            if (blackState.Fallback.Valid)
            {
                Debug.LogWarning($"[BotAI] No obstacle-free shot found — shooting through obstacles. " +
                                $"Blocker={blackState.FallbackBlockerName}");
                return FromCandidate(blackState.Fallback, "OBSTACLE IGNORED (no clear shot found)");
            }

            return null;  // triggers emergency fallback
        }


        private Shot BuildFinalShot(
            FinalShotInfo final, BlackScanState blackState, RedScanState redState,
            Puck[] blackPucks, float strikerRadius, float redPuckRadius,
            bool redOnBoard, HoleLayout layout)
        {
            Vector2 actualFirePos = new Vector2(final.StrikerPos.x, 1.649f);
            Vector2 actualLine2 = final.PuckPos - actualFirePos;
            Vector2 actualLine2Dir = actualLine2.normalized;
            float actualDist2 = actualLine2.magnitude;

            CannonCandidate activeCannon = final.Type == "BANK CANNON SHOT" ? blackState.BankCannon : blackState.Cannon;
            float finalPuckR = ResolvePuckRadius(final, blackPucks, redPuckRadius, redOnBoard, activeCannon);

            float actualCombinedR = finalPuckR + strikerRadius;
            float actualAngle2 = Mathf.Atan2(actualCombinedR, actualDist2) * Mathf.Rad2Deg;
            float actualAngle1Up = Vector2.Angle(Vector2.up, final.HolePos - final.PuckPos);
            float actualScaleFactor = actualAngle1Up > 0.001f ? actualAngle2 / actualAngle1Up : 0f;
            float actualAngle3 = Vector2.Angle(final.HolePos - final.PuckPos, actualLine2);
            float actualAngle4 = Mathf.Clamp(actualAngle3 * actualScaleFactor, 0f, actualAngle2);

            actualAngle4 = ApplyAngleCorrections(final, layout, actualAngle4, activeCannon);

            Vector2 recomputedDir = RecomputeShootDirection(
                final, actualFirePos, actualLine2Dir, actualAngle4, actualCombinedR, layout);

            float shootAngleDeg = Mathf.Atan2(recomputedDir.y, recomputedDir.x) * Mathf.Rad2Deg;

            List<GameObject> puckQueue = BuildPuckQueue(final, blackPucks, activeCannon, redOnBoard);

            return new Shot
            {
                puckQueue = puckQueue,
                angle = shootAngleDeg,
                discPosition = actualFirePos,
                holePosition = final.HolePos,
                type = final.Type
            };
        }

        private float ResolvePuckRadius(
            FinalShotInfo final, Puck[] blackPucks, float redPuckRadius,
            bool redOnBoard, CannonCandidate blackCannon)
        {
            if (final.IsRedTarget && redOnBoard) return redPuckRadius;
            if (final.IsCannonShot || final.Type == "BANK CANNON SHOT") return blackCannon.BlockerR;
            if (final.IsRedCannon) return final.CannonBlockerR;

            Puck nearest = blackPucks
                .OrderBy(x => Vector2.Distance(x.transform.position, final.PuckPos))
                .FirstOrDefault();
            return nearest != null
                ? nearest.GetComponent<CircleCollider2D>().radius * Mathf.Abs(nearest.transform.localScale.x)
                : 0f;
        }

        private float ApplyAngleCorrections(
            FinalShotInfo final, HoleLayout layout, float angle4, CannonCandidate blackCannon)
        {
            bool holeIsBottomLeft = layout.BottomLeft != null && final.HolePos == (Vector2)layout.BottomLeft.position;
            bool holeIsBottomRight = layout.BottomRight != null && final.HolePos == (Vector2)layout.BottomRight.position;
            bool holeIsTopLeft = layout.TopLeft != null && final.HolePos == (Vector2)layout.TopLeft.position;
            bool holeIsTopRight = layout.TopRight != null && final.HolePos == (Vector2)layout.TopRight.position;

            // Bottom corner + short dist1 + A1 near 90°
            if ((holeIsBottomLeft || holeIsBottomRight)
                && Vector2.Distance(final.PuckPos, final.HolePos) < bottomCornerShortDist1Threshold
                && Vector2.Angle(Vector2.up, final.HolePos - final.PuckPos) > bottomCornerNearA1Threshold)
            {
                float scale = holeIsBottomLeft ? (1f - bottomCornerShortAngleScale) : bottomCornerShortAngleScale;
                angle4 *= scale;
                Debug.Log($"[BotAI] Bottom corner angle correction. Side={(holeIsBottomLeft ? "LEFT" : "RIGHT")}  scale={scale:F3}  A4→{angle4:F2}°");
            }

            // ── Normal full: puck near bottom-left corner ─────────────────────────────
            if (final.Type == "NORMAL FULL" && holeIsBottomLeft
                && final.PuckPos.x < -1.2f && final.PuckPos.x >= -1.5f
                && final.PuckPos.y <= -2f)
            {
                angle4 -= 1f;
                Debug.Log($"[BotAI] Bottom-left corner puck correction applied. A4→{angle4:F2}°");
            }

            // ── Normal full: puck near bottom-right corner ────────────────────────────
            if (final.Type == "NORMAL FULL" && holeIsBottomRight
                && final.PuckPos.x >= 1.2f && final.PuckPos.x < 1.5f
                && final.PuckPos.y <= -2f)
            {
                angle4 -= 1f;
                Debug.Log($"[BotAI] Bottom-right corner puck correction applied. A4→{angle4:F2}°");
            }

            // Top corner – near-zero or high puck
            if (holeIsTopLeft || holeIsTopRight)
            {
                bool isNearZero = final.IsNearZeroUp || final.IsNearZeroDown;
                if (isNearZero)
                {
                    float scale = holeIsTopLeft ? (1f - bottomCornerShortAngleScale) : bottomCornerShortAngleScale;
                    angle4 *= scale;
                    Debug.Log($"[BotAI] Top corner near-zero correction. Side={(holeIsTopLeft ? "LEFT" : "RIGHT")}  scale={scale:F3}  A4→{angle4:F2}°");
                }
                else
                {
                    Vector2 checkPos = final.IsCannonShot ? blackCannon.TargetPos : final.PuckPos;
                    if (checkPos.y >= 1.15f)
                    {
                        float scale = holeIsTopLeft ? (1f - highPuckTopHoleAngleScale) : highPuckTopHoleAngleScale;
                        angle4 *= scale;
                        Debug.Log($"[BotAI] High puck top hole correction. PuckY={checkPos.y:F3}  scale={scale:F3}  A4→{angle4:F2}°");
                    }
                }
            }

            return angle4;
        }

        private Vector2 RecomputeShootDirection(
            FinalShotInfo final, Vector2 actualFirePos, Vector2 actualLine2Dir,
            float actualAngle4, float actualCombinedR, HoleLayout layout)
        {
            if (final.Type == "BANK SHOT" || final.Type == "RED BANK SHOT" || final.Type == "BANK CANNON SHOT")
                return final.ShootDir;

            if (final.IsRedCannon)
            {
                Vector2 b2rDir = ((Vector2)GameController.Instance.redPuck.transform.position - final.CannonBlockerPos).normalized;
                Vector2 ghostBall = final.CannonBlockerPos - b2rDir * actualCombinedR;
                return (ghostBall - actualFirePos).normalized;
            }

            if (final.IsCannonShot)
            {
                Vector2 b2tDir = (final.CannonTargetPos - final.CannonBlockerPos).normalized;
                Vector2 ghostBall = final.CannonBlockerPos - b2tDir * actualCombinedR;
                return (ghostBall - actualFirePos).normalized;
            }

            // ── REMOVE the old IsRedTarget && IsNearZeroTopOpposite branch ──
            // It aimed at puckPos - line1Dir*(combinedR*0.95f) — same center-aim problem.

            if (final.Type == "NEAR-ZERO BOTTOM-HOLE (SAME SIDE)")
            {
                Vector2 line1Dir = (final.HolePos - final.PuckPos).normalized;
                Vector2 ghostBall = final.PuckPos - line1Dir * actualCombinedR;
                return (ghostBall - actualFirePos).normalized;
            }

            bool isForcedTopFallback =
                !final.IsRedTarget && !final.IsCannonShot && !final.IsRedCannon &&
                final.PuckPos.y < 1.15f &&
                ((layout.TopLeft != null && final.HolePos == (Vector2)layout.TopLeft.position)
                || (layout.TopRight != null && final.HolePos == (Vector2)layout.TopRight.position));

            if (isForcedTopFallback)
                return (final.PuckPos - actualFirePos).normalized;

            // Standard rotation — now handles nearZeroTopOpp correctly
            Vector2 line1DirFinal = (final.HolePos - final.PuckPos).normalized;
            float cross = actualLine2Dir.x * line1DirFinal.y - actualLine2Dir.y * line1DirFinal.x;
            float signedA4 = actualAngle4 * -Mathf.Sign(cross);
            float rad = signedA4 * Mathf.Deg2Rad;
            float cos4 = Mathf.Cos(rad), sin4 = Mathf.Sin(rad);
            return new Vector2(
                actualLine2Dir.x * cos4 - actualLine2Dir.y * sin4,
                actualLine2Dir.x * sin4 + actualLine2Dir.y * cos4).normalized;
        }

        private List<GameObject> BuildPuckQueue(
            FinalShotInfo final, Puck[] blackPucks, CannonCandidate blackCannon, bool redOnBoard)
        {
            var queue = new List<GameObject>();

            if (final.IsCannonShot || final.Type == "BANK CANNON SHOT")
            {
                queue.Add(final.CannonBlockerGO);
                queue.Add(final.CannonTargetGO);
            }
            else if (final.IsRedCannon)
            {
                queue.Add(final.CannonBlockerGO);
                queue.Add(GameController.Instance.redPuck);
            }
            else if (final.IsRedTarget || final.Type == "RED BANK SHOT")
            {
                queue.Add(GameController.Instance.redPuck);
            }
            else
            {
                Puck nearest = blackPucks
                    .OrderBy(x => Vector2.Distance(x.transform.position, final.PuckPos))
                    .FirstOrDefault();
                if (nearest != null) queue.Add(nearest.gameObject);
            }

            queue.RemoveAll(x => x == null);
            return queue;
        }

        private FinalShotInfo VerifyAndFixOverlap(
            FinalShotInfo final, BlackScanState blackState, RedScanState redState, float strikerRadius)
        {
            Vector2 firePos = new Vector2(final.StrikerPos.x, 1.649f);
            if (IsPositionClear(firePos, strikerRadius)) return final;

            Debug.LogWarning("[BotAI] Final position overlapping — trying fallbacks...");

            // Try each candidate in priority order
            var fallbacksToTry = new (bool valid, Vector2 strikerPos, string label)[]
            {
                (redState.DirectShot.Valid,            redState.DirectShot.StrikerPos,   "RED PUCK MANDATORY FALLBACK"),
                (redState.Cannon.Valid,                redState.Cannon.StrikerPos,       "RED CANNON MANDATORY FALLBACK"),
                (redState.Fallback.Valid,              redState.Fallback.StrikerPos,     "RED PUCK MANDATORY OBSTACLE-IGNORED FALLBACK"),
                (blackState.NearZeroTopOpposite.Valid, blackState.NearZeroTopOpposite.StrikerPos, "NEAR-ZERO TOP FALLBACK"),
                (blackState.NearZeroBottomSame.Valid,  blackState.NearZeroBottomSame.StrikerPos,  "NEAR-ZERO BOTTOM FALLBACK"),
                (blackState.HighPuckForced.Valid,      blackState.HighPuckForced.StrikerPos,      "HIGH PUCK FORCED CORNER FALLBACK"),
                (blackState.Normal.Valid,              blackState.Normal.StrikerPos,     "NORMAL FULL FALLBACK"),
                (blackState.Cannon.Valid,              blackState.Cannon.StrikerPos,     "CANNON SHOT FALLBACK"),
                (blackState.Fallback.Valid,            blackState.Fallback.StrikerPos,   "OBSTACLE IGNORED FALLBACK"),
                (blackState.BankShot.Valid,  blackState.BankShot.StrikerPos,  "BANK SHOT FALLBACK"),
                (redState.BankShot.Valid,    redState.BankShot.StrikerPos,    "RED BANK SHOT FALLBACK"),
                (blackState.BankCannon.Valid, blackState.BankCannon.StrikerPos, "BANK CANNON FALLBACK"),
            };

            foreach (var (valid, pos, label) in fallbacksToTry)
            {
                if (!valid) continue;
                Vector2 candidate = new Vector2(pos.x, 1.649f);
                if (!IsPositionClear(candidate, strikerRadius)) continue;

                Debug.Log($"[BotAI] Fallback selected: {label}");
                final.StrikerPos = pos;

                // ── Recompute shoot direction for the new striker position ────────
                Vector2 newFirePos = new Vector2(pos.x, 1.649f);
                Vector2 newLine2Dir = (final.PuckPos - newFirePos).normalized;
                final.ShootDir = newLine2Dir;  // store for RecomputeShootDirection to use
                // ─────────────────────────────────────────────────────────────────

                return final;
            }

            Debug.LogWarning("[BotAI] All candidates overlapping — proceeding anyway.");
            return final;
        }

        private bool TryEmergencyShot(Puck[] blackPucks, float minX, float maxX)
        {
            Debug.LogWarning("[BotAI] No shot candidates found — using emergency direct shot.");

            Puck emergencyPuck = blackPucks
                .OrderBy(_ => Random.value)
                .FirstOrDefault();

            if (emergencyPuck == null) return false;

            Vector2 puckPos = emergencyPuck.transform.position;
            float emergencyX = puckPos.x < 0f ? 1.24f : -1.24f;
            Vector2 firePos = new Vector2(emergencyX, 1.649f);
            Vector2 dir = (puckPos - firePos).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            selectedShot = new Shot { puckQueue = null, angle = angle, discPosition = firePos, holePosition = Vector2.zero };
            Debug.Log($"[BotAI][EMERGENCY] Target={emergencyPuck.name}  DiscPos={firePos}  Angle={angle:F2}°");
            return true;
        }

        private bool CheckRay(Vector2 origin, Vector2 dir, float dist, GameObject selfPuck, GameObject strikerObj)
        {
            foreach (var h in Physics2D.RaycastAll(origin, dir, dist))
            {
                GameObject go = h.collider.gameObject;
                if (go == selfPuck) continue;
                if (go == strikerObj) continue;
                if (h.collider.CompareTag("Hole")) continue;
                if (h.collider.CompareTag("Wall")) continue;
                if (h.collider.CompareTag("BottomWall")) continue;
                if (h.collider.CompareTag("LeftWall")) continue;
                if (h.collider.CompareTag("RightWall")) continue;
                // Debug.Log($"[BotAI] Raycast hit: {go.name}");
                return true;
            }
            return false;
        }

        private bool CheckRayGetBlocker(Vector2 origin, Vector2 dir, float dist,
                                  GameObject selfPuck, GameObject strikerObj,
                                  out string blockerName)
        {
            blockerName = "";
            foreach (var h in Physics2D.RaycastAll(origin, dir, dist))
            {
                GameObject go = h.collider.gameObject;
                if (go == selfPuck) continue;
                if (go == strikerObj) continue;
                if (h.collider.CompareTag("Hole")) continue;
                blockerName = go.name;
                return true;
            }
            return false;
        }

        private bool CheckRayPucksOnly(Vector2 origin, Vector2 dir, float dist,
                                GameObject selfPuck, GameObject strikerObj)
        {
            foreach (var h in Physics2D.RaycastAll(origin, dir, dist))
            {
                GameObject go = h.collider.gameObject;
                if (go == selfPuck) continue;
                if (go == strikerObj) continue;
                if (h.collider.CompareTag("Hole")) continue;
                if (h.collider.CompareTag("BottomWall")) continue;
                if (h.collider.CompareTag("LeftWall")) continue;
                if (h.collider.CompareTag("RightWall")) continue;
                if (h.collider.CompareTag("Wall")) continue;
                Debug.Log($"[CheckRayPucksOnly] HIT: {go.name}");
                return true;
            }
            return false;
        }

        private bool CheckRayForHoles(Vector2 origin, Vector2 dir, float dist)
        {
            foreach (var h in Physics2D.RaycastAll(origin, dir, dist))
            {
                if (h.collider.CompareTag("Hole")) return true;
            }
            return false;
        }

        private bool IsOverlappingPuck(Vector2 pos, float strikerRadius)
        {
            foreach (Collider2D col in Physics2D.OverlapCircleAll(pos, strikerRadius))
            {
                if (col.gameObject == gameObject) continue;
                string t = col.gameObject.tag;
                if (t == "White" || t == "Black" || t == "Red") return true;
            }
            return false;
        }

        private bool IsPositionClear(Vector2 pos, float strikerRadius)
        {
            foreach (Collider2D col in Physics2D.OverlapCircleAll(pos, strikerRadius))
            {
                if (col.gameObject == gameObject) continue;
                string t = col.gameObject.tag;
                if (t == "White" || t == "Black" || t == "Red") return false;
            }
            return true;
        }

        private bool HasEasyBlackShotExists(
            Puck[] blackPucks, Transform[] holes,
            float minX, float maxX, float scanY, float strikerRadius)
        {
            foreach (Puck puck in blackPucks)
            {
                Vector2 puckPos = puck.transform.position;
                foreach (Transform hole in holes)
                {
                    Vector2 holePos = hole.position;
                    Vector2 line1Dir = (holePos - puckPos).normalized;
                    float dist1 = Vector2.Distance(puckPos, holePos);
                    float off = strikerRadius + 0.05f;

                    if (CheckRay(puckPos + line1Dir * off, line1Dir, dist1 - off, puck.gameObject, gameObject)) continue;

                    for (float x = minX; x <= maxX; x += 0.25f)
                    {
                        Vector2 testPos = new Vector2(x, scanY);
                        Vector2 line2Dir = (puckPos - testPos).normalized;
                        float dist2 = Vector2.Distance(testPos, puckPos);
                        if (!CheckRay(testPos + line2Dir * off, line2Dir, dist2 - off, puck.gameObject, gameObject))
                            return true;
                    }
                }
            }
            return false;
        }

        private void LogFinalShot(FinalShotInfo final, BlackScanState bs, RedScanState rs)
        {
            bool nearlyZeroUp = final.IsNearZeroUp;
            bool nearlyZeroDown = final.IsNearZeroDown;
            float dist1 = Vector2.Distance(final.PuckPos, final.HolePos);

            string a2Min = bs.HasAnyNearZero ? bs.NearZeroA2Min.ToString("F2") : "NA";
            string a2Max = bs.HasAnyNearZero ? bs.NearZeroA2Max.ToString("F2") : "NA";

            Debug.Log(
                $"[BotAI][SHOT] NearlyZero={nearlyZeroUp || nearlyZeroDown} (Up={nearlyZeroUp}, Down={nearlyZeroDown})  " +
                $"A2_min={a2Min}°  A2_max={a2Max}°  " +
                $"A1Up={bs.A1Up:F2}°  A1Down={bs.A1Down:F2}°  type={final.Type}  " +
                $"| A2={bs.A2:F2}°  A3={bs.A3:F2}°  scale={bs.Scale:F4}  dist1(puck→hole)={dist1:F4}"
            );

            Debug.Log($"[BotAI][SHOTDATA] Angle={selectedShot.angle:F2}°  " +
                    $"DiscPos={selectedShot.discPosition}  HolePos={selectedShot.holePosition}");
        }

        private WallInfo[] GatherWalls()
        {
            var walls = new List<WallInfo>();

            GameObject bottomWallGO = GameObject.FindGameObjectWithTag("BottomWall");
            if (bottomWallGO != null)
            {
                Collider2D col = bottomWallGO.GetComponent<Collider2D>();
                if (col != null)
                {
                    Bounds b = col.bounds;
                    walls.Add(new WallInfo { IsVertical = false, Position = b.center.y, Min = b.min.x, Max = b.max.x });
                }
            }

            // GameObject leftWallGO = GameObject.FindGameObjectWithTag("LeftWall");
            // if (leftWallGO != null)
            // {
            //     Collider2D col = leftWallGO.GetComponent<Collider2D>();
            //     if (col != null)
            //     {
            //         Bounds b = col.bounds;
            //         walls.Add(new WallInfo { IsVertical = true, Position = b.center.x, Min = b.min.y, Max = b.max.y });
            //     }
            // }

            // GameObject rightWallGO = GameObject.FindGameObjectWithTag("RightWall");
            // if (rightWallGO != null)
            // {
            //     Collider2D col = rightWallGO.GetComponent<Collider2D>();
            //     if (col != null)
            //     {
            //         Bounds b = col.bounds;
            //         walls.Add(new WallInfo { IsVertical = true, Position = b.center.x, Min = b.min.y, Max = b.max.y });
            //     }
            // }

            return walls.ToArray();
        }

        private LineRenderer BotCreateLine(string objName, Color color, float width)
        {
            width = 0f;
            GameObject go = new GameObject(objName);
            LineRenderer lr = go.AddComponent<LineRenderer>();

            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = color;
            lr.endColor = color;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.sortingLayerName = "Default";
            lr.sortingOrder = 50;
            lr.SetPosition(0, Vector3.zero);
            lr.SetPosition(1, Vector3.zero);

            // Register so DestroyAllBotLines can clean up
            botLines.Add(lr);

            return lr;
        }

        // Call this when a shot fires
        public void DestroyAllBotLines()
        {
            foreach (LineRenderer lr in botLines)
            {
                if (lr != null)
                    Destroy(lr.gameObject);
            }
            botLines.Clear();
        }

        /// <summary>Updates the two endpoints of a debug LineRenderer.</summary>
        private void BotSetLine(LineRenderer lr, Vector2 start, Vector2 end)
        {
            if (lr == null) return;
            // Use a small negative Z so the line draws in front of the board sprite
            lr.SetPosition(0, new Vector3(start.x, start.y, -0.2f));
            lr.SetPosition(1, new Vector3(end.x, end.y, -0.2f));
        }

        IEnumerator MoveDiscToShotPosition(Shot shot)
        {
            float correctX = shot.discPosition.x;
            float fixedY = shot.discPosition.y;
            float fixedZ = transform.localPosition.z;

            bool isCornerShot = Mathf.Abs(correctX - GameController.Instance.playerPuckMaxX) < 0.12f ||
                                Mathf.Abs(correctX - GameController.Instance.playerPuckMinX) < 0.12f;
            bool isHardShot = shot.type.Contains("CANNON") || shot.type.Contains("BANK");

            // ── 1/15: decoy move — slide to wrong spot, fake-aim, abort ──────────────
            if (Random.value < (1f / 15f) && !isCornerShot)
            {
                float decoyX = correctX + Random.Range(0.5f, 1.1f) * (Random.value > 0.5f ? 1f : -1f);
                decoyX = Mathf.Clamp(decoyX, -1.274f, 1.274f);
                yield return StartCoroutine(SlideTo(new Vector3(decoyX, fixedY, fixedZ), Random.Range(0.18f, 0.38f)));
                yield return new WaitForSeconds(Random.Range(0.08f, 0.22f));

                // Brief fake aim
                float fakeAngle = shot.angle + Random.Range(20f, 55f) * (Random.value > 0.5f ? 1f : -1f);
                transform.rotation = Quaternion.Euler(0, 0, fakeAngle);
                bool fakeGrowDone = false;
                float peakScale = Random.Range(0.4f, 0.75f);
                LeanTween.value(0f, peakScale, Random.Range(0.12f, 0.28f))
                    .setEase(LeanTweenType.easeOutQuad)
                    .setOnUpdate(v => arrow.transform.localScale = new Vector3(v * 2.3f, -(v * 2.3f), 1f))
                    .setOnComplete(() => fakeGrowDone = true);
                yield return new WaitUntil(() => fakeGrowDone);

                yield return new WaitForSeconds(Random.Range(0.18f, 0.5f));

                // Abort — shrink arrow
                bool fakeShrinkDone = false;
                LeanTween.value(peakScale, 0f, 0.12f)
                    .setOnUpdate(v => arrow.transform.localScale = new Vector3(v * 2.3f, -(v * 2.3f), 1f))
                    .setOnComplete(() => fakeShrinkDone = true);
                yield return new WaitUntil(() => fakeShrinkDone);
                yield return new WaitForSeconds(Random.Range(0.1f, 0.25f));
            }

            // ── Fake position moves ───────────────────────────────────────────────────
            if (isCornerShot)
            {
                // Corners: go direct, maybe a tiny confident overshoot then settle
                if (Random.value < 0.35f)
                {
                    float nudge = Random.Range(0.02f, 0.07f) * (correctX > 0 ? 1f : -1f);
                    float overshootX = Mathf.Clamp(correctX + nudge, -1.274f, 1.274f);
                    yield return StartCoroutine(SlideTo(new Vector3(overshootX, fixedY, fixedZ), Random.Range(0.15f, 0.28f)));
                    yield return new WaitForSeconds(Random.Range(0.04f, 0.12f));
                }
            }
            else
            {
                // 1/10: chaotic multi-wander — really unsure player
                if (Random.value < 0.1f)
                {
                    int wanders = Random.Range(3, 6);
                    for (int i = 0; i < wanders; i++)
                    {
                        float wanderX = correctX + Random.Range(0.25f, 0.85f) * (Random.value > 0.5f ? 1f : -1f);
                        wanderX = Mathf.Clamp(wanderX, -1.274f, 1.274f);
                        yield return StartCoroutine(SlideTo(new Vector3(wanderX, fixedY, fixedZ), Random.Range(0.12f, 0.38f)));

                        // Occasionally pause mid-wander like rethinking
                        if (Random.value < 0.3f)
                            yield return new WaitForSeconds(Random.Range(0.15f, 0.45f));
                        else
                            yield return new WaitForSeconds(Random.Range(0.04f, 0.14f));
                    }
                }
                else
                {
                    // Normal 1–3 fake moves
                    int fakeMoveCount = Random.Range(1, 4);
                    for (int i = 0; i < fakeMoveCount; i++)
                    {
                        float fakeOffset = Random.Range(0.15f, 0.6f) * (Random.value > 0.5f ? 1f : -1f);
                        float fakeX = Mathf.Clamp(correctX + fakeOffset, -1.274f, 1.274f);
                        yield return StartCoroutine(SlideTo(new Vector3(fakeX, fixedY, fixedZ), Random.Range(0.2f, 0.55f)));

                        // Rarely pause longer mid-fake (player second-guessing)
                        yield return new WaitForSeconds(Random.value < 0.2f
                            ? Random.Range(0.3f, 0.7f)
                            : Random.Range(0.06f, 0.3f));
                    }
                }
            }

            // ── Final approach to correct position ────────────────────────────────────
            float finalSlideDuration = isHardShot ? Random.Range(0.3f, 0.55f) : Random.Range(0.2f, 0.4f);
            yield return StartCoroutine(SlideTo(new Vector3(correctX, fixedY, fixedZ), finalSlideDuration));

            // Tiny micro-settle jitter (hand not perfectly still)
            if (Random.value < 0.4f)
            {
                float jitter = Random.Range(0.005f, 0.02f) * (Random.value > 0.5f ? 1f : -1f);
                yield return StartCoroutine(SlideTo(new Vector3(correctX + jitter, fixedY, fixedZ), 0.06f));
                yield return StartCoroutine(SlideTo(new Vector3(correctX, fixedY, fixedZ), 0.08f));
            }

            // Pre-aim pause (hard shots = longer deliberation)
            float prePause = isHardShot ? Random.Range(0.5f, 1.4f) : Random.Range(0.2f, 1f);
            yield return new WaitForSeconds(prePause);

            // Overlap nudge
            if (isCurrentlyOverlapping)
            {
                float nudgeDir = transform.localPosition.x > 0f ? -1f : 1f;
                while (isCurrentlyOverlapping)
                {
                    Vector3 nudged = transform.localPosition + new Vector3(nudgeDir * 0.02f, 0f, 0f);
                    nudged.x = Mathf.Clamp(nudged.x, -1.274f, 1.274f);
                    yield return StartCoroutine(SlideTo(nudged, 0.01f));
                }
            }

            if (PhotonController.Instance.demoPlayer.isWinning)
            {
                StartCoroutine(FakeLagCoroutine());
            }

            StartCoroutine(PlayAimArrowAnimation(shot.angle, shot.type));

            // Reusable slide helper — moves disc to a target local position over a given duration
            IEnumerator SlideTo(Vector3 targetLocalPos, float duration)
            {
                Vector3 startPos = transform.localPosition;

                // Chance that this movement becomes glitchy instead of smooth
                bool useGlitchMove = Random.value < 0.45f;

                if (useGlitchMove)
                {
                    int glitchSteps = Random.Range(2, 5);

                    for (int i = 0; i < glitchSteps; i++)
                    {
                        float progress = Random.Range(0.15f, 0.9f);

                        Vector3 midPos = Vector3.Lerp(startPos, targetLocalPos, progress);

                        // Small random sideways error so it feels glitchy / unstable
                        midPos.x += Random.Range(-0.12f, 0.12f);
                        midPos.x = Mathf.Clamp(midPos.x, -1.274f, 1.274f);

                        transform.localPosition = midPos;

                        // Very short pause between teleports
                        yield return new WaitForSeconds(Random.Range(0.025f, 0.09f));
                    }

                    // Sometimes do a tiny final smooth settle, sometimes snap directly
                    if (Random.value < 0.5f)
                    {
                        Vector3 settleStart = transform.localPosition;
                        float settleDuration = Random.Range(0.05f, 0.12f);
                        float elapsed = 0f;

                        while (elapsed < settleDuration)
                        {
                            elapsed += Time.deltaTime;
                            float t = Mathf.Clamp01(elapsed / settleDuration);
                            transform.localPosition = Vector3.Lerp(settleStart, targetLocalPos, t);
                            yield return null;
                        }
                    }

                    transform.localPosition = targetLocalPos;
                    yield break;
                }

                // Normal smooth movement
                float elapsedNormal = 0f;
                int easingType = Random.Range(0, 3);

                while (elapsedNormal < duration)
                {
                    elapsedNormal += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsedNormal / duration);

                    float smoothT = easingType switch
                    {
                        0 => Mathf.SmoothStep(0f, 1f, t),
                        1 => 1f - Mathf.Pow(1f - t, 3f),
                        _ => t * t * (3f - 2f * t)
                    };

                    transform.localPosition = Vector3.Lerp(startPos, targetLocalPos, smoothT);
                    yield return null;
                }

                transform.localPosition = targetLocalPos;
            }

            IEnumerator PlayAimArrowAnimation(float targetAngle, string shotType = "")
            {
                if (isCurrentlyOverlapping)
                {
                    yield break;
                }

                bool isHardShot = shotType.Contains("CANNON") || shotType.Contains("BANK");

                // ── Pre-aim think time ────────────────────────────────────────────────────
                float preAimPause = Random.value < 0.7f
                    ? Random.Range(0.05f, 0.3f)
                    : Random.Range(0.5f, 1.1f);
                if (isHardShot) preAimPause += Random.Range(0.25f, 0.7f);
                yield return new WaitForSeconds(preAimPause);

                // ── Setup ─────────────────────────────────────────────────────────────────
                float startOffset = Random.Range(8f, 22f) * (Random.value > 0.5f ? 1f : -1f);
                // Hard shots: start further off — less certain
                if (isHardShot) startOffset *= Random.Range(1.2f, 1.8f);
                float startAngle = targetAngle + startOffset;

                transform.rotation = Quaternion.Euler(0, 0, startAngle);
                arrow.transform.localScale = Vector3.zero;

                // ── Generate corrections — more steps for hard shots ──────────────────────
                int correctionCount = isHardShot ? Random.Range(4, 8) : Random.Range(2, 5);
                float[] correctionAngles = new float[correctionCount];

                for (int i = 0; i < correctionCount; i++)
                {
                    float convergence = (float)(i + 1) / (correctionCount + 1);
                    float driftMultiplier = isHardShot ? Random.Range(1.3f, 2.0f) : 1f;
                    float maxDrift = Mathf.Lerp(Mathf.Abs(startOffset) * 0.5f * driftMultiplier, 2f, convergence);
                    float minDrift = Mathf.Lerp(2f * driftMultiplier, 0.3f, convergence);
                    float drift = Random.Range(minDrift, maxDrift) * (Random.value > 0.5f ? 1f : -1f);
                    correctionAngles[i] = targetAngle + drift;
                }

                // ── Phase 1: Arrow grows + first sweep ───────────────────────────────────
                float growDuration = isHardShot
                    ? Random.Range(0.45f, 0.85f)
                    : (Random.value < 0.65f ? Random.Range(0.28f, 0.5f) : Random.Range(0.55f, 0.9f));

                Quaternion rotFrom = Quaternion.Euler(0, 0, startAngle);
                Quaternion rotTo = Quaternion.Euler(0, 0, correctionAngles[0]);
                bool growDone = false;
                LeanTween.value(0f, 1f, growDuration)
                    .setEase(LeanTweenType.easeOutQuad)
                    .setOnUpdate((float val) =>
                    {
                        arrow.transform.localScale = new Vector3(val * 2.3f, -(val * 2.3f), 1f);
                        transform.rotation = Quaternion.Lerp(rotFrom, rotTo, val);
                    })
                    .setOnComplete(() => growDone = true);
                yield return new WaitUntil(() => growDone);

                // ── Phase 2: Micro-corrections ────────────────────────────────────────────
                for (int i = 1; i < correctionCount; i++)
                {
                    float pauseBetween = isHardShot
                        ? (Random.value < 0.35f ? Random.Range(0.35f, 0.85f) : Random.Range(0.1f, 0.3f))
                        : (Random.value < 0.25f ? Random.Range(0.3f, 0.7f) : Random.Range(0.06f, 0.2f));
                    yield return new WaitForSeconds(pauseBetween);

                    Quaternion corrFrom = transform.rotation;
                    Quaternion corrTo = Quaternion.Euler(0, 0, correctionAngles[i]);
                    float corrSpeed = Random.Range(0.1f, 0.25f) * (1f - (float)i / correctionCount * 0.35f);
                    bool corrDone = false;
                    LeanTween.value(0f, 1f, corrSpeed)
                        .setEase(LeanTweenType.easeInOutSine)
                        .setOnUpdate(val => transform.rotation = Quaternion.Lerp(corrFrom, corrTo, val))
                        .setOnComplete(() => corrDone = true);
                    yield return new WaitUntil(() => corrDone);
                }

                // ── Pre-release pause ─────────────────────────────────────────────────────
                float preReleasePause = Random.value < 0.4f
                    ? Random.Range(0.25f, 0.55f)
                    : Random.Range(0.04f, 0.15f);
                yield return new WaitForSeconds(preReleasePause);

                // ── Final snap to target angle ────────────────────────────────────────────
                Quaternion finalFrom = transform.rotation;
                Quaternion finalTo = Quaternion.Euler(0, 0, targetAngle);
                float finalSpeed = isHardShot ? Random.Range(0.1f, 0.22f) : Random.Range(0.07f, 0.18f);
                bool finalDone = false;
                LeanTween.value(0f, 1f, finalSpeed)
                    .setEase(LeanTweenType.easeInOutSine)
                    .setOnUpdate(val => transform.rotation = Quaternion.Lerp(finalFrom, finalTo, val))
                    .setOnComplete(() => finalDone = true);
                yield return new WaitUntil(() => finalDone);

                // ── Hold on correct angle with subtle breathing oscillation ───────────────
                float holdDuration = Random.Range(0.3f, 0.65f);
                if (isHardShot) holdDuration += Random.Range(0.1f, 0.35f);
                float holdElapsed = 0f;
                float breathFreq = Random.Range(6f, 10f);
                float breathAmp = Random.Range(0.15f, 0.45f);
                while (holdElapsed < holdDuration)
                {
                    holdElapsed += Time.deltaTime;
                    float breathe = Mathf.Sin(holdElapsed * breathFreq) * breathAmp;
                    // Amplitude fades toward zero as we near release
                    float fade = 1f - Mathf.Clamp01(holdElapsed / holdDuration);
                    transform.rotation = Quaternion.Euler(0, 0, targetAngle + breathe * fade);
                    yield return null;
                }
                transform.rotation = Quaternion.Euler(0, 0, targetAngle);

                // ── 1/4 chance: pre-release vibration ────────────────────────────────────
                if (Random.value < 0.25f)
                {
                    float vibDuration = Random.Range(0.6f, 1.1f);
                    float vibElapsed = 0f;
                    float vibFreq = Random.Range(30f, 50f);
                    float vibAmp = Random.Range(0.05f, 0.15f);
                    while (vibElapsed < vibDuration)
                    {
                        vibElapsed += Time.deltaTime;
                        // Intensity builds then fades — like holding back a shot
                        float envelope = Mathf.Sin(Mathf.PI * vibElapsed / vibDuration);
                        float vib = Mathf.Sin(vibElapsed * vibFreq) * vibAmp * envelope;
                        transform.rotation = Quaternion.Euler(0, 0, targetAngle + vib);
                        yield return null;
                    }
                    // Snap clean before firing
                    bool snapDone = false;
                    LeanTween.value(0f, 1f, 0.06f)
                        .setOnUpdate(val => transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(0, 0, targetAngle), val))
                        .setOnComplete(() => snapDone = true);
                    yield return new WaitUntil(() => snapDone);
                    yield return new WaitForSeconds(0.05f);
                }

                if (isCurrentlyOverlapping)
                    Debug.LogWarning("[BotAI] Final aim position overlapping — firing anyway.");

                FireShot();
            }
        }

        // 7. Fires the striker at full force
        void FireShot()
        {
            string[] shots = {
                "NORMAL FULL",
                "OBSTACLE IGNORED (no clear shot found)",
                "RED PUCK SHOT (MANDATORY — last black)",
                "NEAR-ZERO BOTTOM-HOLE (SAME SIDE)",
                "NEAR-ZERO TOP-HOLE (OPPOSITE SIDE)",
                "RED PUCK SHOT",
                "HIGH PUCK FORCED CORNER",
                "CANNON SHOT"    
            };
            
            if (selectedShot != null && shots.Contains(selectedShot.type) && selectedShot.puckQueue != null && selectedShot.puckQueue.Count > 0)
            {
                Vector2 puckPos = selectedShot.puckQueue[0].transform.position;
                if (puckPos.x > -1.95f && puckPos.x < 1.95f && puckPos.y > -0.75f && puckPos.y < 0.75f)
                {
                    collider.radius = 0.45f;
                }
            }

            arrow.SetActive(false);
            forceMultiplier = GameController.Instance.hasBroken ? 1.3f : 1.5f;
            Shoot();
        }

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                stream.SendNext(rb.linearVelocity);
                stream.SendNext(GameController.Instance.playerSlider.value);
                stream.SendNext(arrow.activeInHierarchy);
                stream.SendNext(arrow.transform.localScale);
                stream.SendNext(arrow.transform.localRotation);
                stream.SendNext(arrow.GetComponent<SpriteRenderer>().enabled);
                // stream.SendNext(collider.isTrigger);
            }
            else
            {
                lastVel = (Vector2)stream.ReceiveNext();
                opponentSliderValue = (float)stream.ReceiveNext();
                arrow.SetActive((bool)stream.ReceiveNext());
                arrowLocalScale = (Vector3)stream.ReceiveNext();
                arrow.transform.localRotation = (Quaternion)stream.ReceiveNext();
                arrow.GetComponent<SpriteRenderer>().enabled = (bool)stream.ReceiveNext();
                // collider.isTrigger = (bool)stream.ReceiveNext();
            }
        }

        // ✅ Updated OnTriggerEnter2D - matches Puck.cs animation exactly
        void OnTriggerEnter2D(Collider2D col)
        {
            if (col.CompareTag("Hole"))
            {
                // ✅ Check if pocketing is allowed
                if (!GameController.Instance.allowPocketing)
                {
                    return;
                }

                // Capture velocity before physics is disabled
                float pocketV = rb.linearVelocity.magnitude;

                // Play pocket sound with velocity (matches Puck.cs)
                if (!PhotonController.Instance.offlineMatch)
                {
                    photonView.RPC("PlayPocketSoundRPC", RpcTarget.All, pocketV, transform.position);
                }
                else
                {
                    PlayPocketSoundRPC(pocketV, transform.position);
                }

                // Disable physics immediately
                GetComponent<CircleCollider2D>().enabled = false;
                rb.linearVelocity = Vector2.zero;
                rb.simulated = false;
                lastVel = Vector2.zero;
                isMoving = false;

                // Hide shadow (if player puck has one)
                if (!PhotonController.Instance.offlineMatch)
                {
                    photonView.RPC("HideShadowRPC", RpcTarget.All);
                }
                else
                {
                    HideShadowRPC();
                }

                LeanTween.move(gameObject, col.transform.position, 0.1f).setOnComplete(() =>
                {
                    LeanTween.color(gameObject, new Color32(0, 0, 0, 100), 0.1f).setOnComplete(() =>
                    {
                        LeanTween.color(gameObject, new Color32(0, 0, 0, 0), 0.2f);
                    });
                });

                if (!PhotonController.Instance.offlineMatch)
                {
                    if (!photonView.IsMine) return;
                    GetComponent<PhotonView>().RPC("PuckOnHole", RpcTarget.AllBuffered, gameObject.tag, gameObject.name);
                }
                else
                {
                    PuckOnHole(gameObject.tag, gameObject.name);
                }
            }
        }

        private void OnCollisionEnter2D(Collision2D other)
        {
            if (PhotonController.Instance.offlineMatch && selectedShot != null && selectedShot.puckQueue != null)
            {
                if (other.gameObject.name == selectedShot.puckQueue[0].name)
                {
                    correctPuckHit = true;
                }
                else if (other.gameObject.CompareTag("BottomWall") && selectedShot.type == "BANK SHOT" && !correctWallHit)
                {
                    correctWallHit = true;
                    GameObject targetPuck = selectedShot.puckQueue[0];
                    float puckRadius = targetPuck.GetComponent<CircleCollider2D>().radius
                                    * Mathf.Abs(targetPuck.transform.localScale.x);

                    ApplyCollisionVelocityTowardBankPuck(
                        other.relativeVelocity,
                        targetPuck.transform.position,
                        selectedShot.holePosition,
                        puckRadius);
                }
                else if (other.gameObject.CompareTag("BottomWall") && selectedShot.type == "BANK CANNON SHOT" && !correctWallHit)
                {
                    correctWallHit = true;
                    GameObject blockerPuck = selectedShot.puckQueue[0];
                    GameObject targetPuck = selectedShot.puckQueue[1];

                    float blockerRadius = blockerPuck.GetComponent<CircleCollider2D>().radius * Mathf.Abs(blockerPuck.transform.localScale.x);
                    float myRadius = GetComponent<CircleCollider2D>().radius * Mathf.Abs(transform.localScale.x);
                    float combinedR = blockerRadius + myRadius;

                    Vector2 b2tDir = ((Vector2)targetPuck.transform.position - (Vector2)blockerPuck.transform.position).normalized;
                    Vector2 ghostBall = (Vector2)blockerPuck.transform.position - b2tDir * combinedR;

                    Vector2 dir = (ghostBall - rb.position).normalized;
                    float speed = other.relativeVelocity.magnitude;
                    rb.linearVelocity = Vector2.zero;
                    rb.AddForce(dir * speed * rb.mass, ForceMode2D.Impulse);
                }
                else if ((other.gameObject.CompareTag("LeftWall") || other.gameObject.CompareTag("RightWall"))
                        && selectedShot.type == "BANK SHOT" && !correctWallHit)
                {
                    correctWallHit = true;
                    GameObject targetPuck = selectedShot.puckQueue[0];
                    float puckRadius = targetPuck.GetComponent<CircleCollider2D>().radius
                                    * Mathf.Abs(targetPuck.transform.localScale.x);
                    ApplyCollisionVelocityTowardBankPuck(
                        other.relativeVelocity,
                        targetPuck.transform.position,
                        selectedShot.holePosition,
                        puckRadius);
                }
                else if ((other.gameObject.CompareTag("LeftWall") || other.gameObject.CompareTag("RightWall"))
                        && selectedShot.type == "BANK CANNON SHOT" && !correctWallHit)
                {
                    correctWallHit = true;
                    GameObject blockerPuck = selectedShot.puckQueue[0];
                    GameObject targetPuck = selectedShot.puckQueue[1];

                    float blockerRadius = blockerPuck.GetComponent<CircleCollider2D>().radius * Mathf.Abs(blockerPuck.transform.localScale.x);
                    float myRadius = GetComponent<CircleCollider2D>().radius * Mathf.Abs(transform.localScale.x);
                    float combinedR = blockerRadius + myRadius;

                    Vector2 b2tDir = ((Vector2)targetPuck.transform.position - (Vector2)blockerPuck.transform.position).normalized;
                    Vector2 ghostBall = (Vector2)blockerPuck.transform.position - b2tDir * combinedR;

                    Vector2 dir = (ghostBall - rb.position).normalized;
                    float speed = other.relativeVelocity.magnitude;
                    rb.linearVelocity = Vector2.zero;
                    rb.AddForce(dir * speed * rb.mass, ForceMode2D.Impulse);
                }
                else if (selectedShot.type != "BANK CANNON SHOT" && selectedShot.type != "BANK SHOT" && other.gameObject.name != "BottomWall" && !correctPuckHit)
                {
                    selectedShot.puckQueue = null;
                }
            }
        }

        void ApplyCollisionVelocityTowardBankPuck(Vector2 collisionVelocity, Vector2 puckPosition, Vector2 holePosition, float puckRadius)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;

            Vector2 dir;

            if (puckPosition.x > 1.9f || puckPosition.x < -1.9f)
            {
                // Puck is near a side wall — aim directly at puck center
                dir = (puckPosition - rb.position).normalized;
            }
            else
            {
                float strikerRadius = GetComponent<CircleCollider2D>().radius * Mathf.Abs(transform.localScale.x);
                float combinedR = puckRadius + strikerRadius;

                Vector2 puckToHole = (holePosition - puckPosition).normalized;
                Vector2 ghostBall = puckPosition - puckToHole * combinedR;
                dir = (ghostBall - rb.position).normalized;
            }

            float speed = collisionVelocity.magnitude;
            rb.linearVelocity = Vector2.zero;
            rb.AddForce(dir * speed * rb.mass, ForceMode2D.Impulse);
        }

        [PunRPC]
        void PlayPocketSoundRPC(float velocity, Vector3 pos)
        {
            PlayPocketSound(DiskPotClip, velocity, pos);
        }

        [PunRPC]
        void HideShadowRPC()
        {
            // Check if player puck has a shadow child (index 0)
            if (transform.childCount > 0)
            {
                Debug.Log("B: Hiding shadow");
                GameObject shad = transform.GetChild(0).gameObject;
                shad.SetActive(false);
            }
        }


        [PunRPC]
        void ShowShadowRPC()
        {
            // Check if player puck has a shadow child (index 0)
            if (transform.childCount > 0)
            {
                GameObject shad = transform.GetChild(0).gameObject;
                shad.SetActive(true);
                shad.GetComponent<SpriteRenderer>().color = new Color32(0, 0, 0, 203);
            }
        }


        [PunRPC]
        void PuckOnHole(string puckTag, string puckName)
        {
            GameController.Instance.PuckOnHole(puckTag, puckName);
        }

        void OnDisable()
        {
            LeanTween.cancelAll();
        }

        public void SetRepelMode(bool on)
        {
            repelModeActive = on;

            if (on)
            {
                if (repelCoroutine != null) StopCoroutine(repelCoroutine);
                repelCoroutine = StartCoroutine(RepelFromHoles());
            }
            else
            {
                if (repelCoroutine != null)
                {
                    StopCoroutine(repelCoroutine);
                    repelCoroutine = null;
                }
                rb.linearDamping = repelBaseDamping;
            }
        }

        private IEnumerator RepelFromHoles()
        {
            Transform[] holes = GameObject.FindGameObjectsWithTag("Hole")
                .Select(h => h.transform)
                .ToArray();

            Debug.Log($"Hole Count: {holes.Length}");

            while (repelModeActive)
            {
                float closestDist = float.MaxValue;

                foreach (Transform hole in holes)
                {
                    float dist = Vector2.Distance(transform.position, hole.position);
                    if (dist < closestDist)
                        closestDist = dist;
                }

                if (closestDist < repelStartDistance)
                {
                    // 0 when at repelStartDistance, 1 when at distance 0
                    float t = 1f - Mathf.Clamp01(closestDist / repelStartDistance);
                    rb.linearDamping = Mathf.Lerp(repelBaseDamping, repelMaxDamping, t);
                }
                else
                {
                    rb.linearDamping = repelBaseDamping;
                }

                yield return null; // runs every frame
            }
        }

    }
}