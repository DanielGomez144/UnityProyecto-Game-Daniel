using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(NavMeshAgent))]
public class GuardAI : MonoBehaviour
{
    public enum State { Patrol, Suspicious, Alert, Disabled }

    [Header("Patrulla")]
    public Transform[] waypoints;
    public float patrolSpeed = 2f;
    public float waitAtWaypoint = 2f;

    [Header("Visión")]
    public Transform player;                 // si queda vacío, busca el objeto con tag "Player"
    public float viewDistance = 12f;
    [Range(10f, 180f)] public float viewAngle = 90f;
    public float eyeHeight = 1.6f;
    public LayerMask obstacleMask = ~0;
    [Range(0.2f, 1f)] public float crouchVisionMultiplier = 0.6f;

    [Header("Detección")]
    public float detectionRate = 0.6f;       // cuánto sube por segundo al verte
    public float hearingRate = 0.3f;         // cuánto sube por segundo al oírte
    public float decayRate = 0.2f;           // cuánto baja por segundo si no sabe nada
    public float suspicionThreshold = 0.4f;  // a partir de aquí sospecha; a 1 entra en alerta

    [Header("Comportamiento")]
    public float investigateSpeed = 3f;
    public float investigateWait = 3f;
    public float chaseSpeed = 5.5f;
    public float loseTime = 4f;              // segundos sin verte para salir de alerta
    public float catchDistance = 1.3f;
    public bool restartOnCatch = true;

    [Header("Indicador visual (opcional)")]
    public Renderer indicator;

    public State CurrentState { get; private set; } = State.Patrol;
    public float Detection { get; private set; }
    public System.Action<State> OnStateChanged;   // útil para el audio

    NavMeshAgent agent;
    PlayerController playerCtrl;
    Vector3 lastKnownPosition;
    float timeSinceSeen;
    float waitTimer;
    float investigateTimer;
    int waypointIndex;
    bool waiting;
    bool caught;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (player != null) playerCtrl = player.GetComponent<PlayerController>();
    }

    void Start()
    {
        if (waypoints != null && waypoints.Length > 0)
            agent.SetDestination(waypoints[0].position);
        UpdateIndicator();
    }

    void Update()
    {
        if (CurrentState == State.Disabled || player == null || caught) return;

        float dist = Vector3.Distance(transform.position, player.position);
        bool sees = CanSeePlayer(dist);
        bool hears = playerCtrl != null && playerCtrl.NoiseRadius > 0f && dist <= playerCtrl.NoiseRadius;

        if (sees)
        {
            float closeness = 1f - Mathf.Clamp01(dist / viewDistance);
            Detection += detectionRate * (0.5f + closeness) * Time.deltaTime;
            lastKnownPosition = player.position;
            timeSinceSeen = 0f;
        }
        else
        {
            timeSinceSeen += Time.deltaTime;
            if (hears)
            {
                Detection += hearingRate * Time.deltaTime;
                lastKnownPosition = player.position;
            }
            else if (CurrentState != State.Alert)
            {
                Detection -= decayRate * Time.deltaTime;
            }
        }
        Detection = Mathf.Clamp01(Detection);

        switch (CurrentState)
        {
            case State.Patrol:     UpdatePatrol(); break;
            case State.Suspicious: UpdateSuspicious(sees); break;
            case State.Alert:      UpdateAlert(sees, dist); break;
        }
    }

    // ---------- Estados ----------

    void UpdatePatrol()
    {
        if (Detection >= 1f) { SetState(State.Alert); return; }
        if (Detection >= suspicionThreshold) { SetState(State.Suspicious); return; }

        if (waypoints == null || waypoints.Length == 0) return;
        agent.speed = patrolSpeed;

        if (waiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                waiting = false;
                waypointIndex = (waypointIndex + 1) % waypoints.Length;
                agent.SetDestination(waypoints[waypointIndex].position);
            }
        }
        else if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f)
        {
            waiting = true;
            waitTimer = waitAtWaypoint;
        }
    }

    void UpdateSuspicious(bool sees)
    {
        if (Detection >= 1f) { SetState(State.Alert); return; }

        agent.speed = investigateSpeed;
        agent.SetDestination(lastKnownPosition);

        bool reached = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f;
        if (reached)
        {
            FaceTowards(lastKnownPosition);
            investigateTimer += Time.deltaTime;
            if (investigateTimer >= investigateWait && Detection <= 0.05f)
                ReturnToPatrol();
        }
        else if (sees)
        {
            investigateTimer = 0f;
        }
    }

    void UpdateAlert(bool sees, float dist)
    {
        agent.speed = chaseSpeed;
        agent.SetDestination(sees ? player.position : lastKnownPosition);

        if (dist <= catchDistance && sees)
        {
            Caught();
            return;
        }

        if (!sees && timeSinceSeen >= loseTime)
        {
            Detection = 0.6f;
            SetState(State.Suspicious);
        }
    }

    void ReturnToPatrol()
    {
        investigateTimer = 0f;
        waiting = false;
        SetState(State.Patrol);
        if (waypoints != null && waypoints.Length > 0)
            agent.SetDestination(waypoints[waypointIndex].position);
    }

    void SetState(State newState)
    {
        if (CurrentState == newState) return;
        CurrentState = newState;
        investigateTimer = 0f;
        UpdateIndicator();
        OnStateChanged?.Invoke(newState);
    }

    // ---------- Visión ----------

    bool CanSeePlayer(float dist)
    {
        float maxDist = viewDistance;
        bool crouching = playerCtrl != null && playerCtrl.IsCrouching;
        if (crouching) maxDist *= crouchVisionMultiplier;
        if (dist > maxDist) return false;

        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 target = player.position + Vector3.up * (crouching ? 0.6f : 1.0f);
        Vector3 dir = target - eye;

        Vector3 flat = new Vector3(dir.x, 0f, dir.z);
        if (Vector3.Angle(transform.forward, flat) > viewAngle * 0.5f) return false;

        if (Physics.Linecast(eye, target, out RaycastHit hit, obstacleMask, QueryTriggerInteraction.Ignore))
            return hit.transform.IsChildOf(player);

        return true;
    }

    void FaceTowards(Vector3 point)
    {
        Vector3 d = point - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude < 0.01f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 4f * Time.deltaTime);
    }

    // ---------- API pública (para el asesinato silencioso) ----------

    public void Disable()
    {
        if (CurrentState == State.Disabled) return;
        agent.isStopped = true;
        agent.ResetPath();
        SetState(State.Disabled);
        // Aquí después irá la animación de caer
    }

    // ---------- Utilidades ----------

    void Caught()
    {
        caught = true;
        agent.isStopped = true;
        Debug.Log("¡Te atraparon!");
        if (restartOnCatch) Invoke(nameof(RestartScene), 1.5f);
    }

    void RestartScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void UpdateIndicator()
    {
        if (indicator == null) return;
        Color c = Color.green;
        switch (CurrentState)
        {
            case State.Suspicious: c = Color.yellow; break;
            case State.Alert:      c = Color.red; break;
            case State.Disabled:   c = Color.gray; break;
        }
        indicator.material.color = c;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Gizmos.color = Color.yellow;
        Vector3 left = Quaternion.Euler(0f, -viewAngle * 0.5f, 0f) * transform.forward;
        Vector3 right = Quaternion.Euler(0f, viewAngle * 0.5f, 0f) * transform.forward;
        Gizmos.DrawLine(eye, eye + left * viewDistance);
        Gizmos.DrawLine(eye, eye + right * viewDistance);
        Gizmos.DrawWireSphere(transform.position, viewDistance);
    }
}