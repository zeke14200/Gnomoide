using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instancia { get; private set; }

    [Header("Status da Sala (Apenas Leitura)")]
    public int totalMoedas;
    public int moedasColetadas;
    public bool allCoinsCollected;

    public int moedasRestantes => totalMoedas - moedasColetadas;

    void Awake()
    {
        if (Instancia == null)
        {
            Instancia = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        SecretCoin[] moedasNaSala = FindObjectsByType<SecretCoin>(FindObjectsSortMode.None);      
        totalMoedas = moedasNaSala.Length;
        moedasColetadas = 0;
        allCoinsCollected = (totalMoedas == 0); // Prevenção de bug se a sala nascer vazia

    }

    public void RegistrarColeta()
    {
        moedasColetadas++;
        
        Debug.Log($"[GameManager] Moeda coletada! Faltam {moedasRestantes}.");

        if (moedasColetadas >= totalMoedas)
        {
            allCoinsCollected = true;
            Debug.Log("[GameManager] TODAS AS MOEDAS COLETADAS! Retorne ao ponto inicial.");
        }
    }
}