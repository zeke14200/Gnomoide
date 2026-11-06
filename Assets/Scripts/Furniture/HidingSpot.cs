using UnityEngine;
using System.Collections.Generic;

public class HidingSpot : MonoBehaviour
{
    [Header("Configurações do Esconderijo")]
    public bool permiteEsconderijo = true;
    
    [Tooltip("Distância extra que o jogador deve se afastar para a parede voltar a existir.")]
    public float margemDeSaida = 0.5f;

    [Tooltip("Deixe vazio! O script encontra as paredes sólidas sozinho.")]
    public Collider[] colisoresSolidos;

    private Rigidbody rb;
    private Collider triggerDetecao;
    private bool jogadorEstaDentro = false;

    private Transform transformJogador;
    private PlayerHiding scriptHidingDoJogador;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        Collider[] todosMeusColliders = GetComponents<Collider>();
        foreach (Collider col in todosMeusColliders)
        {
            if (col.isTrigger) 
            {
                triggerDetecao = col;
                break;
            }
        }

        if (colisoresSolidos == null || colisoresSolidos.Length == 0)
        {
            List<Collider> solidos = new List<Collider>();
            foreach (Collider col in todosMeusColliders)
            {
                if (!col.isTrigger) solidos.Add(col);
            }
            colisoresSolidos = solidos.ToArray();
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!permiteEsconderijo || !EstaEstavel()) return;

        if (other.isTrigger) return;

        if (other.CompareTag("Player"))
        {
            if (jogadorEstaDentro) return;

            PlayerMovement pm = other.GetComponent<PlayerMovement>();
            
            if (pm != null && pm.isCrouching)
            {
                jogadorEstaDentro = true;
                
                transformJogador = other.transform;
                scriptHidingDoJogador = other.GetComponent<PlayerHiding>();

                TornarIntangivel(true);
                
                if (scriptHidingDoJogador != null && !scriptHidingDoJogador.isHidden)
                {
                    scriptHidingDoJogador.EntrarEsconderijo();
                }
            }
        }
    }

    void Update()
    {
        if (jogadorEstaDentro && transformJogador != null && triggerDetecao != null)
        {
            Vector3 bordaDoMovel = triggerDetecao.ClosestPoint(transformJogador.position);
            float distanciaParaFora = Vector3.Distance(transformJogador.position, bordaDoMovel);

            if (distanciaParaFora > margemDeSaida)
            {
                SairDoEsconderijo();
            }
        }
    }

    private void SairDoEsconderijo()
    {
        jogadorEstaDentro = false;
        
        if (scriptHidingDoJogador != null && scriptHidingDoJogador.isHidden)
        {
            scriptHidingDoJogador.SairEsconderijo();
        }
        
        TornarIntangivel(false);

        transformJogador = null;
        scriptHidingDoJogador = null;
    }

    private void TornarIntangivel(bool intangivel)
    {
        if (rb != null) rb.isKinematic = intangivel;

        for (int i = 0; i < colisoresSolidos.Length; i++)
        {
            if (colisoresSolidos[i] != null)
            {
                colisoresSolidos[i].enabled = !intangivel;
            }
        }
    }

    public bool EstaEstavel()
    {
        if (rb == null) return true; 
        return rb.linearVelocity.sqrMagnitude < 0.1f && rb.angularVelocity.sqrMagnitude < 0.1f;
    }
}