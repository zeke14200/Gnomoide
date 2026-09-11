using UnityEngine;

public class VidaJogador : MonoBehaviour
{
    public int vidaMaxima = 100;
    public int vidaAtual;

    void Start()
    {
        vidaAtual = vidaMaxima; 
    }

    public void TomarDano(int quantidade)
    {
        vidaAtual -= quantidade;
        Debug.Log("O jogador tomou " + quantidade + " de dano! Vida restante: " + vidaAtual);

        if (vidaAtual <= 0)
        {
            Morrer();
        }
    }

    void Morrer()
    {
        Debug.Log("Jogador Morreu!");
    }
}