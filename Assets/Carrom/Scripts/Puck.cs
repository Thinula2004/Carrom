using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BEKStudio
{
    public class Puck : MonoBehaviourPun, IPunObservable
    {
        public PhotonView photonView;
        Rigidbody2D rb;
        SpriteRenderer spriteRenderer;
        Vector2 puckStartPos;
        Vector2 lastVel;
        public bool practiceMode;
        public bool isMoving;
        public string whichArea = GameController.BOTTOM_LEFT_SECTION;

        // ✅ Prevent double pocketing
        public bool isPocketed = false;
        private bool isProcessingPocket = false;

        [SerializeField] private AudioSource[] childAudios;

        [Header("Velocity Settings")]
        [SerializeField] private float minVelocity = 0.5f;
        [SerializeField] private float maxVelocity = 10f;
        [SerializeField] private float minVolume = 0.3f;
        [SerializeField] private float maxVolume = 1f;
        [SerializeField] private float minPitch = 0.8f;
        [SerializeField] private float maxPitch = 1.3f;

        [Header("Breaking sounds")]
        public AudioClip BreakSmall;
        public AudioClip BreakMedium;
        public AudioClip BreakHard;

        [Header("Normal shooting sounds")]
        public AudioClip shootSmall;
        public AudioClip shootMedium;
        public AudioClip shootHard;

        [Header("collide with coin sounds")]
        public AudioClip collideSmall;
        public AudioClip collideMedium;
        public AudioClip collideHard;

        public AudioClip coinPot;

        void Start()
        {
            rb = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            puckStartPos = transform.localPosition;

            childAudios = GetComponentsInChildren<AudioSource>();

            lastVel = Vector2.zero;
            isMoving = false;

            // Initialize pocket state
            isPocketed = false;
            isProcessingPocket = false;

            try
            {
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            }
            catch
            {
            }

            if (practiceMode)
            {
                ResetPosition();
            }
        }

        void Update()
        {
            if (isPocketed)
            {
                isMoving = false;
            }
            else
            {
                isMoving = lastVel != Vector2.zero;
            }
        }

        private float GetVolumeFromVelocityLocal(float velocity)
        {
            float normalized = Mathf.InverseLerp(minVelocity, maxVelocity, velocity);
            float volume = Mathf.Lerp(minVolume, maxVolume, normalized);
            return Mathf.Clamp01(volume);
        }

        public void PlayShootSound(AudioClip clip, float velocity, Vector3 pos)
        {
            if (clip == null) return;
            float volume = GetVolumeFromVelocityLocal(velocity);
            float fixedPitch = 1f;

            if (SFXManager.Instance != null)
            {
                SFXManager.Instance.PlayAtPosition(clip, pos, volume, fixedPitch, 0f);
                return;
            }

            if (childAudios != null && childAudios.Length > 0 && childAudios[0] != null)
            {
                childAudios[0].PlayOneShot(clip, volume);
            }
        }

        public void PlayPocketSound(AudioClip clip, float velocity, Vector3 pos)
        {
            if (clip == null) return;
            float volume = GetVolumeFromVelocityLocal(velocity);
            float fixedPitch = 1f;

            if (SFXManager.Instance != null)
            {
                SFXManager.Instance.PlayAtPosition(clip, pos, volume, fixedPitch, 0f);
                return;
            }

            if (childAudios != null && childAudios.Length > 1 && childAudios[1] != null)
            {
                childAudios[1].PlayOneShot(clip, volume);
            }
        }

        public void PlayCollideSound(AudioClip clip, float velocity, Vector3 pos)
        {
            if (clip == null) return;
            float volume = GetVolumeFromVelocityLocal(velocity);
            float fixedPitch = 1f;

            if (SFXManager.Instance != null)
            {
                SFXManager.Instance.PlayAtPosition(clip, pos, volume, fixedPitch, 0f);
                return;
            }

            if (childAudios != null && childAudios.Length > 2 && childAudios[2] != null)
            {
                childAudios[2].PlayOneShot(clip, volume);
            }
        }

        public void BreakSmallShot(float v, Vector3 pos) { PlayShootSound(BreakSmall, v, pos); }
        public void BreakMediumShot(float v, Vector3 pos) { PlayShootSound(BreakMedium ?? BreakSmall, v, pos); }
        public void BreakHardShot(float v, Vector3 pos) { PlayShootSound(BreakHard ?? BreakSmall, v, pos); }

        public void ShootSmallShot(float v, Vector3 pos) { PlayShootSound(shootSmall, v, pos); }
        public void ShootMediumShot(float v, Vector3 pos) { PlayShootSound(shootMedium ?? shootSmall, v, pos); }
        public void ShootHardShot(float v, Vector3 pos) { PlayShootSound(shootHard ?? shootSmall, v, pos); }

        public void CollideSmallShot(float v, Vector3 pos) { PlayCollideSound(collideSmall, v, pos); }
        public void CollideMediumShot(float v, Vector3 pos) { PlayCollideSound(collideMedium ?? collideSmall, v, pos); }
        public void CollideHardShot(float v, Vector3 pos) { PlayCollideSound(collideHard ?? collideSmall, v, pos); }

        public void CoinPotSound(float v, Vector3 pos) { PlayPocketSound(coinPot, v, pos); }

        void FixedUpdate()
        {
            if (photonView.IsMine)
            {
                lastVel = rb.linearVelocity;

                if (lastVel.magnitude < 0.01f && rb.linearVelocity.magnitude < 0.01f)
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                }
            }
        }

        void OnCollisionEnter2D(Collision2D collision)
        {
            string tag = collision.gameObject.tag;

            Vector3 contactPos = transform.position;
            if (collision.contacts != null && collision.contacts.Length > 0)
                contactPos = collision.contacts[0].point;

            if (tag == "White" || tag == "Black" || tag == "Red")
            {
                float velocity = collision.relativeVelocity.magnitude;

                if (velocity >= minVelocity)
                {
                    string soundType = GetSoundTypeFromVelocity(velocity);
                    if (!PhotonController.Instance.offlineMatch)
                    {
                        photonView.RPC("PlayCollideSoundRPC", RpcTarget.All, soundType, velocity, contactPos);
                    }
                    else
                    {
                        PlayCollideSoundRPC(soundType, velocity, contactPos);
                    }
                }

                if (PhotonController.Instance.offlineMatch && PlayerPuck.Instance.selectedShot != null && PlayerPuck.Instance.selectedShot.puckQueue != null && PlayerPuck.Instance.selectedShot.puckQueue.Count > 1 && collision.gameObject.name == PlayerPuck.Instance.selectedShot.firstPuck.name && PlayerPuck.Instance.selectedShot.targetPuck.name == gameObject.name)
                {
                    Debug.Log("S: Cannon Shot Target Puck Hit");
                    GameObject firstPuck = collision.gameObject;
                    if (PlayerPuck.Instance.selectedShot.type == "BANK CANNON SHOT")
                    {
                        firstPuck.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                    }
                    ApplyCollisionVelocityTowardHole(collision.relativeVelocity, PlayerPuck.Instance.selectedShot.holePosition);
                }

            }
            else if (tag == "Player" && PlayerPuck.Instance.chanceCount == 0)
            {
                GameController.Instance.hasBroken = true;
                float velocity = collision.relativeVelocity.magnitude;
                if (velocity >= minVelocity)
                {
                    string soundType = GetSoundTypeFromVelocity(velocity);
                    if (!PhotonController.Instance.offlineMatch)
                    {
                        photonView.RPC("PlayBreakSoundRPC", RpcTarget.All, soundType, velocity, contactPos);
                    }
                    else
                    {
                        PlayBreakSoundRPC(soundType, velocity, contactPos);
                    }
                }

            }
            else if (tag == "Player" && PlayerPuck.Instance.chanceCount > 0)
            {
                float velocity = collision.relativeVelocity.magnitude;

                if (velocity >= minVelocity)
                {
                    string soundType = GetSoundTypeFromVelocity(velocity);
                    if (!PhotonController.Instance.offlineMatch)
                    {
                        photonView.RPC("PlayShootSoundRPC", RpcTarget.All, soundType, velocity, contactPos);
                    }
                    else
                    {
                        PlayShootSoundRPC(soundType, velocity, contactPos);
                    }
                }
                if (GameController.Instance.hasBroken && PhotonController.Instance.offlineMatch)
                {
                    if (PlayerPuck.Instance.selectedShot != null && PlayerPuck.Instance.selectedShot.targetPuck != null && PlayerPuck.Instance.selectedShot.puckQueue != null)
                    {
                        if (PlayerPuck.Instance.selectedShot.targetPuck.name == gameObject.name)
                        {
                            Debug.Log($"S: Direct Shot Hit Hole Pos {PlayerPuck.Instance.selectedShot.holePosition}");
                            ApplyCollisionVelocityTowardHole(collision.relativeVelocity, PlayerPuck.Instance.selectedShot.holePosition);

                        }
                        else if (PlayerPuck.Instance.selectedShot.puckQueue.Count > 1 &&
                                PlayerPuck.Instance.selectedShot.firstPuck.name == gameObject.name)
                        {
                            Debug.Log("S: Cannon Shot First Puck Hit");

                            ApplyCollisionVelocityTowardCannonTarget(
                                collision.relativeVelocity,
                                PlayerPuck.Instance.selectedShot.targetPuck,
                                PlayerPuck.Instance.selectedShot.holePosition
                            );
                        }
                    }
                }
                GameController.Instance.hasBroken = true;
            }
        }

        void ApplyCollisionVelocityTowardHole(Vector2 collisionVelocity, Vector2 holePosition)
        {
            if (!PhotonController.Instance.demoPlayer.isWinning)
            {
                return;
            }
            Debug.Log("Applying velocity to target");
            rb.bodyType = RigidbodyType2D.Dynamic;

            Vector2 dir = (holePosition - rb.position).normalized;

            float speed = collisionVelocity.magnitude;

            rb.linearVelocity = Vector2.zero;

            rb.AddForce(dir * speed * rb.mass, ForceMode2D.Impulse);
            PlayerPuck.Instance.SetRepelMode(true);
            GameController.Instance.WhitePuckDamping(true);
        }

        void ApplyCollisionVelocityTowardCannonTarget(Vector2 collisionVelocity, GameObject targetPuck, Vector2 holePosition)
        {
            if (!PhotonController.Instance.demoPlayer.isWinning)
            {
                return;
            }

            if (targetPuck == null)
            {
                return;
            }

            Debug.Log("Applying cannon ghost-ball velocity to first puck");

            rb.bodyType = RigidbodyType2D.Dynamic;

            Vector2 targetPos = targetPuck.transform.position;

            CircleCollider2D myCollider = GetComponent<CircleCollider2D>();
            CircleCollider2D targetCollider = targetPuck.GetComponent<CircleCollider2D>();

            if (myCollider == null || targetCollider == null)
            {
                Vector2 fallbackDir = (targetPos - rb.position).normalized;
                float fallbackSpeed = collisionVelocity.magnitude;

                rb.linearVelocity = Vector2.zero;
                rb.AddForce(fallbackDir * fallbackSpeed * rb.mass, ForceMode2D.Impulse);
                return;
            }

            float myRadius = myCollider.radius * Mathf.Abs(transform.localScale.x);
            float targetRadius = targetCollider.radius * Mathf.Abs(targetPuck.transform.localScale.x);
            float combinedRadius = myRadius + targetRadius;

            Vector2 targetToHoleDir = (holePosition - targetPos).normalized;

            if (targetToHoleDir.sqrMagnitude < 0.0001f)
            {
                targetToHoleDir = (targetPos - rb.position).normalized;
            }

            // This is the point where the first puck's center should reach
            // to hit the target puck from the correct side.
            Vector2 ghostBallPoint = targetPos - targetToHoleDir * combinedRadius;

            Vector2 dir = (ghostBallPoint - rb.position).normalized;
            float speed = collisionVelocity.magnitude;

            rb.linearVelocity = Vector2.zero;
            rb.AddForce(dir * speed * rb.mass, ForceMode2D.Impulse);

            GameController.Instance.WhitePuckDamping(true);
        }

        [PunRPC]
        void PlayBreakSoundRPC(string soundType, float velocity, Vector3 pos)
        {
            switch (soundType)
            {
                case "Small":
                    BreakSmallShot(velocity, pos);
                    break;
                case "Medium":
                    BreakMediumShot(velocity, pos);
                    break;
                case "Hard":
                    BreakHardShot(velocity, pos);
                    break;
                default:
                    BreakSmallShot(velocity, pos);
                    break;
            }
        }

        [PunRPC]
        void PlayShootSoundRPC(string soundType, float velocity, Vector3 pos)
        {
            switch (soundType)
            {
                case "Small":
                    ShootSmallShot(velocity, pos);
                    break;
                case "Medium":
                    ShootMediumShot(velocity, pos);
                    break;
                case "Hard":
                    ShootHardShot(velocity, pos);
                    break;
                default:
                    ShootSmallShot(velocity, pos);
                    break;
            }
        }

        [PunRPC]
        void PlayCollideSoundRPC(string soundType, float velocity, Vector3 pos)
        {
            switch (soundType)
            {
                case "Small":
                    CollideSmallShot(velocity, pos);
                    break;
                case "Medium":
                    CollideMediumShot(velocity, pos);
                    break;
                case "Hard":
                    CollideHardShot(velocity, pos);
                    break;
                default:
                    CollideSmallShot(velocity, pos);
                    break;
            }
        }

        [PunRPC]
        void PlayPocketSoundRPC(float velocity, Vector3 pos)
        {
            CoinPotSound(velocity, pos);
        }

        private string GetSoundTypeFromVelocity(float velocity)
        {
            float normalizedVelocity = Mathf.InverseLerp(minVelocity, maxVelocity, velocity);

            if (normalizedVelocity < 0.33f)
                return "Small";
            else if (normalizedVelocity < 0.66f)
                return "Medium";
            else
                return "Hard";
        }

        private float GetVolumeFromVelocity(float velocity)
        {
            float normalized = Mathf.InverseLerp(minVelocity, maxVelocity, velocity);
            float volume = Mathf.Lerp(minVolume, maxVolume, normalized);
            return Mathf.Clamp01(volume);
        }

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                stream.SendNext(rb.linearVelocity);
                stream.SendNext(rb.simulated);
                stream.SendNext(spriteRenderer.color.a);
                stream.SendNext(isPocketed); // Sync pocket state
            }
            else
            {
                lastVel = (Vector2)stream.ReceiveNext();
                rb.simulated = (bool)stream.ReceiveNext();
                spriteRenderer.color = new Color(1, 1, 1, (float)stream.ReceiveNext());
                isPocketed = (bool)stream.ReceiveNext(); // Receive pocket state
            }
        }

        // --- Self-heal for stale pocket state ---
        private void TryRecoverPocketState()
        {
            // If the puck is on the board with collider enabled but marked pocketed/processing, clear the stale flags
            if ((isPocketed || isProcessingPocket) &&
                gameObject.activeInHierarchy &&
                GetComponent<Collider2D>().enabled)
            {
                isPocketed = false;
                isProcessingPocket = false;
            }
        }

        void OnTriggerEnter2D(Collider2D col)
        {
            if (col.CompareTag("TopDiscArea"))
            {
                if (PhotonController.Instance.offlineMatch)
                {
                    Rigidbody2D rb = GetComponent<Rigidbody2D>();
                    if (rb == null) return;

                    // If object is already moving
                    if (rb.linearVelocity.sqrMagnitude > 0.1f)
                    {
                        Vector2 dir = rb.linearVelocity.normalized;

                        float pushForce = .01f; // tweak this
                        rb.AddForce(dir * pushForce, ForceMode2D.Impulse);
                    }
                }
            }
            if (col.CompareTag("Hole"))
            {
                // Recover if a stale state slipped through
                TryRecoverPocketState();

                // Check if pocketing is allowed
                if (!GameController.Instance.allowPocketing)
                {
                    return;
                }

                // If still marked pocketed/processing, skip (will recover on next overlap)
                if (isPocketed || isProcessingPocket)
                {
                    return;
                }

                // Mark as being processed immediately
                isProcessingPocket = true;

                float pocketV = rb.linearVelocity.magnitude;
                if (!PhotonController.Instance.offlineMatch)
                {
                    photonView.RPC("PlayPocketSoundRPC", RpcTarget.All, pocketV, transform.position);
                    photonView.RPC("MarkAsPocketedRPC", RpcTarget.All);
                    photonView.RPC("HideShadowRPC", RpcTarget.All);
                }
                else
                {
                    PlayPocketSoundRPC(pocketV, transform.position);
                    MarkAsPocketedRPC();
                    HideShadowRPC();
                }

                // Disable collider IMMEDIATELY to prevent re-trigger
                GetComponent<CircleCollider2D>().enabled = false;
                rb.linearVelocity = Vector2.zero;
                rb.simulated = false;
                lastVel = Vector2.zero;
                isMoving = false;

                // Fade out the coin
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
        // Ensure coins already in the hole get processed when pocketing becomes allowed
        void OnTriggerStay2D(Collider2D col)
        {
            if (col.CompareTag("Section"))
            {
                if (col.gameObject.name == GameController.BOTTOM_RIGHT_SECTION)
                {
                    whichArea = GameController.BOTTOM_RIGHT_SECTION;

                }
                else if (col.gameObject.name == GameController.BOTTOM_LEFT_SECTION)
                {
                    whichArea = GameController.BOTTOM_LEFT_SECTION;

                }
                else if (col.gameObject.name == GameController.TOP_LEFT_SECTION)
                {
                    whichArea = GameController.TOP_LEFT_SECTION;

                }
                else if (col.gameObject.name == GameController.TOP_RIGHT_SECTION)
                {
                    whichArea = GameController.TOP_RIGHT_SECTION;
                }
            }

            if (!col.CompareTag("Hole")) return;

            // Recover if a stale state slipped through
            TryRecoverPocketState();

            if (!GameController.Instance.allowPocketing) return;
            if (isPocketed || isProcessingPocket) return;

            OnTriggerEnter2D(col);
        }

        // Mark puck as pocketed on all clients
        [PunRPC]
        void MarkAsPocketedRPC()
        {
            isPocketed = true;
            isProcessingPocket = true;
        }

        [PunRPC]
        void HideShadowRPC()
        {
            GameObject shad = transform.GetChild(0).gameObject;
            shad.SetActive(false);
        }

        public void ResetPosition()
        {
            // Reset pocket state when repositioning
            LeanTween.cancel(gameObject);
            isPocketed = false;
            isProcessingPocket = false;

            if (practiceMode)
            {
                transform.localPosition = new Vector2(Random.Range(-1.89f, 1.89f), Random.Range(-1.16f, 1.16f));
            }
            else
            {
                if (photonView.IsMine)
                {
                    transform.localPosition = puckStartPos;
                }
            }

            GetComponent<CircleCollider2D>().enabled = true;
            rb.linearVelocity = Vector2.zero;
            rb.simulated = true;
            spriteRenderer.color = new Color32(255, 255, 255, 255);
            if (!PhotonController.Instance.offlineMatch)
            {
                photonView.RPC("ShowShadowRPC", RpcTarget.All);
            }
            else
            {
                ShowShadowRPC();
            }
            lastVel = Vector2.zero;
            isMoving = false;
        }

        // Network-forced reset so any client can restore a pocketed puck (used for red reset)
        [PunRPC]
        public void ForceResetPositionRPC()
        {
            LeanTween.cancel(gameObject);
            isPocketed = false;
            isProcessingPocket = false;

            // Force position reset on all clients to avoid owner-only moves blocking the reset
            transform.localPosition = puckStartPos;

            GetComponent<CircleCollider2D>().enabled = true;
            rb.linearVelocity = Vector2.zero;
            rb.simulated = true;
            spriteRenderer.color = new Color32(255, 255, 255, 255);
            if (!PhotonController.Instance.offlineMatch)
            {
                photonView.RPC("ShowShadowRPC", RpcTarget.All);
            }
            else
            {
                ShowShadowRPC();
            }
            lastVel = Vector2.zero;
            isMoving = false;
        }

        [PunRPC]
        void ShowShadowRPC()
        {
            GameObject shad = transform.GetChild(0).gameObject;
            shad.SetActive(true);
            shad.GetComponent<SpriteRenderer>().color = new Color32(0, 0, 0, 203);
        }

        [PunRPC]
        void PuckOnHole(string puckTag, string puckName)
        {
            GameController.Instance.PuckOnHole(puckTag, puckName);
        }

        // public string isBordered()
        // {
        //     Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, .3f);
        //     if(hits.Length <= 0)
        //     {
        //         return GameController.NO_WALL;
        //     }
        //     foreach (Collider2D col in hits)
        //     {
        //         if (col.gameObject.CompareTag("SideWall"))
        //         {
        //             if(col.gameObject.name == GameController.LEFT_WALL)
        //             {
        //                 return GameController.LEFT_WALL;
        //             }else if(col.gameObject.name == GameController.RIGHT_WALL)
        //             {
        //                 return GameController.RIGHT_WALL;
        //             }else if(col.gameObject.name == GameController.TOP_WALL)
        //             {
        //                 return GameController.TOP_WALL;
        //             }else if(col.gameObject.name == GameController.BOTTOM_WALL)
        //             {
        //                 return GameController.BOTTOM_WALL;
        //             }
        //         }
        //     }
        //     return GameController.NO_WALL;
        // }

    }
}