using UnityEngine;
using UnityEngine.SceneManagement; 

[RequireComponent(typeof(Collider))]
public class StartPoint : MonoBehaviour
{
    private bool estaReiniciando = false;

    void Start()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (estaReiniciando) return;

        if (other.CompareTag("Player"))
        {
            if (GameManager.Instancia != null && GameManager.Instancia.allCoinsCollected)
            {
                estaReiniciando = true;
                Debug.Log("[StartPoint] Objetivo concluído! Reiniciando a sala...");
                ReiniciarSala();
            }
            else
            {
                int faltam = GameManager.Instancia != null ? GameManager.Instancia.moedasRestantes : 0;
                Debug.Log($"[StartPoint] Você ainda precisa coletar {faltam} moedas.");
            }
        }
    }

    private void ReiniciarSala()
    {
        Scene cenaAtual = SceneManager.GetActiveScene();
        SceneManager.LoadScene(cenaAtual.name);
    }
}