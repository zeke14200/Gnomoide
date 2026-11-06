using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NavMeshObstacle))] 
public class FurniturePhysics : MonoBehaviour
{
    [Header("Configurações de Interação")]
    [Tooltip("Se desmarcado, o objeto nunca será derrubado (ex: paredes ou armários pesados).")]
    public bool canBeKnockedOver = true;

    [Tooltip("Velocidade mínima do jogador ou inimigo (impactForce) necessária para liberar a física.")]
    public float resistanceThreshold = 14f;

    [Tooltip("Multiplicador da força bruta aplicada ao objeto no momento do impacto.")]
    public float impactMultiplier = 2f;

    private Rigidbody rb;
    private NavMeshObstacle obstaculoNavMesh;
    private bool isPhysicsReleased = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        obstaculoNavMesh = GetComponent<NavMeshObstacle>();
        
        obstaculoNavMesh.carving = true; 
        obstaculoNavMesh.carveOnlyStationary = true; 

        LockPhysics();
    }

    private void LockPhysics()
    {
        rb.constraints = RigidbodyConstraints.FreezePositionX | 
                         RigidbodyConstraints.FreezePositionZ | 
                         RigidbodyConstraints.FreezeRotation;
        isPhysicsReleased = false;
    }

    public void ReceiveImpact(Vector3 impactDirection, float impactForce, bool isSpecialMove, Vector3 hitPoint)
    {
        if (!canBeKnockedOver || isPhysicsReleased) return;

        if (!isSpecialMove) return;

        if (impactForce < resistanceThreshold) return;

        ReleasePhysics(impactDirection, impactForce, hitPoint);
    }

    public void IA_ForceReleasePhysics(Vector3 impactDirection, float impactForce, Vector3 hitPoint)
    {
        if (!canBeKnockedOver || isPhysicsReleased) return;
        ReleasePhysics(impactDirection, impactForce, hitPoint);
    }

    private void ReleasePhysics(Vector3 impactDirection, float impactForce, Vector3 hitPoint)
    {
        rb.constraints = RigidbodyConstraints.None;
        isPhysicsReleased = true;

        Vector3 forceToApply = (impactDirection + (Vector3.up * 0.3f)).normalized * (impactForce * impactMultiplier);

        rb.AddForceAtPosition(forceToApply, hitPoint, ForceMode.Impulse);
    }
}