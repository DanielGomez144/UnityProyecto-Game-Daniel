using UnityEngine;

public class AssassinationController : MonoBehaviour
{
    [Header("Ajustes")]
    public float range = 1.8f;                 // distancia máxima al guardia
    [Range(10f, 90f)] public float backAngle = 60f;   // qué tan "por la espalda" debes estar
    public KeyCode key = KeyCode.E;

    [Header("Audio (opcional)")]
    public AudioSource audioSource;
    public AudioClip killSound;

    GuardAI target;
    GUIStyle promptStyle;

    void Update()
    {
        target = FindTarget();
        if (target != null && Input.GetKeyDown(key))
            Kill(target);
    }

    GuardAI FindTarget()
    {
        GuardAI best = null;
        float bestDist = range;

        foreach (GuardAI g in GuardAI.All)
        {
            // Solo se puede eliminar a quien no está en alerta ni ya eliminado
            if (g.CurrentState == GuardAI.State.Alert || g.CurrentState == GuardAI.State.Disabled)
                continue;

            Vector3 toGuard = g.transform.position - transform.position;
            toGuard.y = 0f;
            float d = toGuard.magnitude;
            if (d > bestDist) continue;

            // Estás detrás si la dirección jugador -> guardia coincide con hacia dónde mira el guardia
            if (Vector3.Angle(g.transform.forward, toGuard) > backAngle) continue;

            best = g;
            bestDist = d;
        }
        return best;
    }

    void Kill(GuardAI guard)
    {
        // Mirar al guardia
        Vector3 look = guard.transform.position - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(look);

        guard.Disable();

        if (audioSource != null && killSound != null)
            audioSource.PlayOneShot(killSound);
    }

    void OnGUI()
    {
        if (target == null) return;

        if (promptStyle == null)
        {
            promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            promptStyle.normal.textColor = Color.white;
        }

        GUI.Label(new Rect(0f, Screen.height * 0.7f, Screen.width, 40f),
                  "[" + key + "] Eliminar en silencio", promptStyle);
    }
}
