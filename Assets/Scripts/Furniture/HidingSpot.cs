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

        // Encontra o Trigger (A zona de detecção gordinha que DEVE ter 'Is Trigger' marcado no Inspector)
        Collider[] todosMeusColliders = GetComponents<Collider>();
        foreach (Collider col in todosMeusColliders)
        {
            if (col.isTrigger) 
            {
                triggerDetecao = col;
                break;
            }
        }

        // Encontra a parede de concreto (o BoxCollider que NÃO é Trigger)
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

        // Ignora colisores que sejam apenas Triggers (ex: radares da IA)
        if (other.isTrigger) return;

        if (other.CompareTag("Player"))
        {
            if (jogadorEstaDentro) return;

            PlayerMovement pm = other.GetComponent<PlayerMovement>();
            
            // A entrada EXIGE o Shift
            if (pm != null && pm.isCrouching)
            {
                jogadorEstaDentro = true;
                
                transformJogador = other.transform;
                scriptHidingDoJogador = other.GetComponent<PlayerHiding>();

                // MÁGICA DA INTANGIBILIDADE ABSOLUTA: Desliga a parede e congela a gravidade!
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

            // O esconderijo SÓ É CANCELADO quando o jogador estiver totalmente fora da bolha do móvel
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
        
        // Devolve a parede e a gravidade
        TornarIntangivel(false);

        transformJogador = null;
        scriptHidingDoJogador = null;
    }

    private void TornarIntangivel(bool intangivel)
    {
        // 1. Congela o móvel no ar. Ele não vai cair no void porque está imune à gravidade.
        if (rb != null) rb.isKinematic = intangivel;

        // 2. Desliga a parede física completamente. É IMPOSSÍVEL o CharacterController bater nela agora.
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