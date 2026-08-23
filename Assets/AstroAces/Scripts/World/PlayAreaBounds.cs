using UnityEngine;
using AstroAces.UI;

namespace AstroAces.World
{
    /// <summary>
    /// Soft play-area boundary -- BUILD_PLAN.md 6.6, DESIGN.md Sec 9: 5km x 5km, a warning
    /// message plus a gentle nudge back toward centre outside it. Explicitly "no invisible
    /// wall" -- this adds a force on top of whatever the aircraft's own flight forces are
    /// already doing, it never teleports, clamps position, or otherwise overrides the player.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayAreaBounds : MonoBehaviour
    {
        [SerializeField] float halfSizeMeters = 2500f;   // 5km x 5km centred on the origin
        [SerializeField] float pushForceNewtons = 8000f;
        [SerializeField] float warningIntervalSeconds = 3f;

        Rigidbody rb;
        MessageLog messageLog;   // optional -- fine if absent (e.g. an enemy aircraft)
        float warningTimer;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            messageLog = GetComponent<MessageLog>();
        }

        void FixedUpdate()
        {
            Vector3 pos = transform.position;
            bool outOfBounds = Mathf.Abs(pos.x) > halfSizeMeters || Mathf.Abs(pos.z) > halfSizeMeters;
            if (!outOfBounds)
            {
                warningTimer = 0f;
                return;
            }

            Vector3 towardCentre = new Vector3(-pos.x, 0f, -pos.z).normalized;
            rb.AddForce(towardCentre * pushForceNewtons);

            warningTimer -= Time.fixedDeltaTime;
            if (warningTimer <= 0f)
            {
                warningTimer = warningIntervalSeconds;
                messageLog?.Show("RETURN TO PLAY AREA");
            }
        }
    }
}
