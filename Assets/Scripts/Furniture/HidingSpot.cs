using UnityEngine;

[RequireComponent(typeof(Collider))]
public class HidingSpot : MonoBehaviour
{
    [Header("Configurações do Esconderijo")]
    public bool permiteEsconderijo = true;
    
    [Tooltip("Arraste para cá os Colliders sólidos (que agora estão nos GameObjects filhos)")]
    public Collider[] colisoresSolidos;

    private Rigidbody rb;
    private int camadaIntangivel;
    private int[] camadasOriginais;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        
        // Busca o ID numérico da Layer que criamos no Editor
        camadaIntangivel = LayerMask.NameToLayer("MovelIntangivel");

        // Salva a Layer original de cada pedaço da parede para poder devolver depois
        camadasOriginais = new int[colisoresSolidos.Length];
        for (int i = 0; i < colisoresSolidos.Length; i++)
        {
            if (colisoresSolidos[i] != null)
            {
                camadasOriginais[i] = colisoresSolidos[i].gameObject.layer;
            }
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
                if (pm.isCrouching || ph.isHidden)
                {
                    // Transforma as paredes na layer "MovelIntangivel"
                    TornarIntangivel(true);
                    
                    if (!ph.isHidden)
                    {
                        ph.EntrarEsconderijo();
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
                if (ph.isHidden)
                {
                    ph.SairEsconderijo();
                }
                
                // Devolve as paredes para a Layer original (ex: Default)
                TornarIntangivel(false);
            }
        }
    }

    private void TornarIntangivel(bool intangivel)
    {
        if (camadaIntangivel == -1)
        {
            Debug.LogError("ERRO: A Layer 'MovelIntangivel' não existe! Crie ela na Unity.");
            return;
        }

        for (int i = 0; i < colisoresSolidos.Length; i++)
        {
            if (colisoresSolidos[i] != null)
            {
                // Se for pra ficar intangível, aplica a layer fantasma. Se não, restaura a original.
                colisoresSolidos[i].gameObject.layer = intangivel ? camadaIntangivel : camadasOriginais[i];
            }
        }
    }

    public bool EstaEstavel()
    {
        if (rb == null) return true; 
        return rb.linearVelocity.sqrMagnitude < 0.1f && rb.angularVelocity.sqrMagnitude < 0.1f;
    }
}