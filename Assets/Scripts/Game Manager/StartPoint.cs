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
        if (aguardandoSaidaDoJogador) return;

        if (other.CompareTag("Player"))
        {
            if (GameManager.Instancia != null && GameManager.Instancia.allCoinsCollected)
            {
                aguardandoSaidaDoJogador = true; 
                
                Debug.Log("[StartPoint] Voltando à base com sucesso. Iniciando próximo round...");
                GameManager.Instancia.AvancarRound();
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            aguardandoSaidaDoJogador = false;
        }
    }
}