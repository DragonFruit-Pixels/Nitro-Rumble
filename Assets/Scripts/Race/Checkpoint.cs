using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        Racer racer = other.GetComponentInParent<Racer>();
        if (racer == null) return;

        Logger.Log($"[Checkpoint] {racer.name} entró a {gameObject.name}");
        RaceManager.Instance.NotifyCheckpoint(racer, this);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // Volumen real del trigger. El eje fino del box es su Z local: si no queda cruzando
        // la pista, el auto pasa por al lado y el checkpoint no se registra nunca. Dibujarlo
        // acá es la única forma de ver ese error de autoría sin seleccionar el objeto.
        if (TryGetComponent(out BoxCollider box))
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);

            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.15f);
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            Gizmos.DrawWireCube(box.center, box.size);

            // Flecha sobre el forward: tiene que apuntar en la dirección de marcha.
            // CarRamDestroy.GetRespawnRotation la usa para orientar el auto al revivir.
            Vector3 tip = box.center + Vector3.forward * (box.size.z * 0.5f + 2.5f);
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(box.center, tip);
            Gizmos.DrawLine(tip, tip + new Vector3( 0.6f, 0f, -0.9f));
            Gizmos.DrawLine(tip, tip + new Vector3(-0.6f, 0f, -0.9f));

            Gizmos.matrix = Matrix4x4.identity;
        }

        // Respawn position (misma lógica que CarRamDestroy.GetRespawnPosition)
        Vector3 respawnPos = transform.position + Vector3.up * 0.5f;

        // Dirección flat (misma lógica que CarRamDestroy.GetRespawnRotation)
        Vector3 flat = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (flat.sqrMagnitude < 0.01f) flat = Vector3.forward;
        Quaternion respawnRot = Quaternion.LookRotation(flat, Vector3.up);

        // Esfera en el punto de respawn
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
        Gizmos.DrawSphere(respawnPos, 0.35f);

        // Footprint del auto
        Gizmos.color = new Color(1f, 1f, 1f, 0.2f);
        Gizmos.matrix = Matrix4x4.TRS(respawnPos, respawnRot, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(1.8f, 1f, 3.5f));
        Gizmos.matrix = Matrix4x4.identity;

        // Flecha de dirección
        Gizmos.color = Color.green;
        Gizmos.DrawRay(respawnPos, flat * 2.5f);

        // Label con nombre
        UnityEditor.Handles.Label(
            respawnPos + Vector3.up * 0.8f,
            gameObject.name,
            new GUIStyle
            {
                normal    = { textColor = new Color(0.2f, 0.8f, 1f) },
                fontStyle = FontStyle.Bold,
                fontSize  = 12
            });
    }
#endif
}
