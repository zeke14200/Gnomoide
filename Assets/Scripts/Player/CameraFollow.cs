using UnityEngine;

// Mudei o nome da classe para CameraFollow para evitar conflito com a classe nativa "Camera" da Unity
public class CameraFollow : MonoBehaviour 
{
    [Header("Referências")]
    public Transform jogador;

    [Header("Configurações Gerais")]
    public Vector3 offset; 
    public bool suavizarMovimento = true;
    public float velocidadeSuavizacao = 5f;

    [Header("Limites de Visão (Boundaries)")]
    public bool usarLimites = true;
    [Tooltip("X = Mínimo (Esquerda), Y = Máximo (Direita)")]
    public Vector2 limiteX = new Vector2(-100f, 100f); 
    [Tooltip("X = Mínimo (Baixo), Y = Máximo (Cima)")]
    public Vector2 limiteZ = new Vector2(-5f, 100f); 

    void Start()
    {
        EncontrarJogador();
    }

    void LateUpdate()
    {
        if (jogador == null)
        {
            EncontrarJogador();
            return; 
        }

        // Calcula a posição desejada baseada no jogador + offset
        Vector3 posicaoAlvo = jogador.position + offset;

        // Trava matematicamente a posição alvo dentro dos limites definidos
        if (usarLimites)
        {
            posicaoAlvo.x = Mathf.Clamp(posicaoAlvo.x, limiteX.x, limiteX.y);
            posicaoAlvo.z = Mathf.Clamp(posicaoAlvo.z, limiteZ.x, limiteZ.y);
        }

        // Aplica o movimento
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