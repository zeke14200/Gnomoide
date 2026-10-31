using UnityEngine;

[RequireComponent(typeof(Collider))]
public class StartPoint : MonoBehaviour
{
    private bool aguardandoSaidaDoJogador = true;

    void Start()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        // Evita que o round passe instantaneamente no exato frame em que o jogador for teleportado para cá
        if (aguardandoSaidaDoJogador) return;

        if (other.CompareTag("Player"))
        {
            if (GameManager.Instancia != null && GameManager.Instancia.allCoinsCollected)
            {
                // Impede que o jogador fique quicando e ative o round várias vezes
                aguardandoSaidaDoJogador = true; 
                
                Debug.Log("[StartPoint] Voltando à base com sucesso. Iniciando próximo round...");
                GameManager.Instancia.AvancarRound();
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        // Assim que o jogador der o primeiro passo para fora da base no início do round, a base fica "armada" para receber ele de volta
        if (other.CompareTag("Player"))
        {
            aguardandoSaidaDoJogador = false;
        }
    }
}