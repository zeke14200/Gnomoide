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
        if (jogador != null)
        {
            offset = transform.position - jogador.position;
        }
    }

    void LateUpdate()
    {
        if (jogador == null) return;

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
}