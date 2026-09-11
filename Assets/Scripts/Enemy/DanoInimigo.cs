using UnityEngine;

public class DanoInimigo : MonoBehaviour
{
    public int danoDoAtaque = 10;

    void OnCollisionEnter(Collision colisao)
    {
        TentarAplicarDano(colisao.gameObject);
    }

    void TentarAplicarDano(GameObject alvo)
    {
        if (alvo.TryGetComponent<VidaJogador>(out VidaJogador scriptDeVida))
        {
            scriptDeVida.TomarDano(danoDoAtaque);
        }
    }
}