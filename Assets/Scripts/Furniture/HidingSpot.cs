using UnityEngine;

[RequireComponent(typeof(Collider))]
public class HidingSpot : MonoBehaviour
{
    [Header("Configurações do Esconderijo")]
    public bool permiteEsconderijo = true;
    
    [Tooltip("Pode deixar vazio! O script agora encontra as paredes sólidas sozinho.")]
    public Collider[] colisoresSolidos;

    private Rigidbody rb;
    private bool jogadorEstaDentro = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        
        // Automação inteligente: Acha todas as paredes sólidas do móvel sozinho
        // Assim você não precisa ficar arrastando colisores no Inspector toda hora
        if (colisoresSolidos == null || colisoresSolidos.Length == 0)
        {
            Collider[] todos = GetComponentsInChildren<Collider>();
            System.Collections.Generic.List<Collider> solidos = new System.Collections.Generic.List<Collider>();
            foreach (Collider col in todos)
            {
                if (!col.isTrigger) solidos.Add(col); // O que não for Trigger é parede!
            }
            colisoresSolidos = solidos.ToArray();
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!permiteEsconderijo || !EstaEstavel()) return;

        if (other.CompareTag("Player"))
        {
            PlayerMovement pm = other.GetComponent<PlayerMovement>();
            PlayerHiding ph = other.GetComponent<PlayerHiding>();

            if (pm != null && ph != null)
            {
                // A mágica: Se segurar Shift OU já estiver escondido (soltou o Shift lá dentro)
                if (pm.isCrouching || ph.isHidden)
                {
                    // Essa trava garante que ele só execute a rotina de entrar uma única vez
                    if (!jogadorEstaDentro)
                    {
                        jogadorEstaDentro = true;
                        TornarIntangivel(true);
                        
                        if (!ph.isHidden) ph.EntrarEsconderijo();
                    }
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerHiding ph = other.GetComponent<PlayerHiding>();

            if (ph != null)
            {
                // O jogador saiu fisicamente da zona do Trigger
                jogadorEstaDentro = false;
                
                if (ph.isHidden) ph.SairEsconderijo();
                
                TornarIntangivel(false);
            }
        }
    }

    private void TornarIntangivel(bool intangivel)
    {
        // O Pulo do Gato: Para o móvel não cair no infinito sem a colisão sólida, 
        // nós congelamos a física dele temporariamente (isKinematic)
        if (rb != null) rb.isKinematic = intangivel;

        // Desliga ou liga as paredes de vez para o CharacterController poder passar liso
        foreach (Collider solido in colisoresSolidos)
        {
            if (solido != null)
            {
                solido.enabled = !intangivel; 
            }
        }
    }

    public bool EstaEstavel()
    {
        if (rb == null) return true; 
        return rb.linearVelocity.sqrMagnitude < 0.1f && rb.angularVelocity.sqrMagnitude < 0.1f;
    }
}