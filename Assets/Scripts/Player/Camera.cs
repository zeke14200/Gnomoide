using UnityEngine;

public class Camera : MonoBehaviour
{
    [Header("Referências")]
    public Transform jogador;

    [Header("Configurações")]
    public Vector3 offset; 
    public bool suavizarMovimento = true;
    public float velocidadeSuavizacao = 5f;

    void Start()
    {
        // Tenta achar o jogador automaticamente se estiver vazio
        EncontrarJogador();
    }

    void LateUpdate()
    {
        // Se o jogador foi destruído (fim de round anterior), tenta achar o novo clone recém-criado
        if (jogador == null)
        {
            EncontrarJogador();
            return; // Pula este frame para evitar erro de cálculo de posição
        }

        Vector3 posicaoAlvo = jogador.position + offset;

        if (suavizarMovimento)
        {
            transform.position = Vector3.Lerp(transform.position, posicaoAlvo, velocidadeSuavizacao * Time.deltaTime);
        }
        else
        {
            transform.position = posicaoAlvo;
        }
    }

    private void EncontrarJogador()
    {
        GameObject objPlayer = GameObject.FindGameObjectWithTag("Player");
        if (objPlayer != null)
        {
            jogador = objPlayer.transform;
            
            if (offset == Vector3.zero)
            {
                offset = transform.position - jogador.position;
            }
        }
    }
}