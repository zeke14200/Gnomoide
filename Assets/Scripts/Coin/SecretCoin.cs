using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SecretCoin : MonoBehaviour
{
    [Header("Configurações de Revelação")]
    public float raioRevelacao = 5f;
    public float duracaoFade = 1.5f;

    [Header("Configurações da Recompensa")]
    public int valorMoeda = 1;

    [Header("Efeitos Visuais e Sonoros")]
    public GameObject vfxColeta;
    public AudioClip sfxColeta;
    public float velocidadeRotacao = 90f;
    public float velocidadeFlutuacao = 2f;
    public float alturaFlutuacao = 0.25f;

    private Transform jogador;
    private Renderer renderizador;
    private Collider colisor;
    private MaterialPropertyBlock propBlock;

    private bool revelada = false;
    private bool coletada = false;
    private float tempoFade = 0f;
    private Vector3 posicaoInicial;

    void Start()
    {
        colisor = GetComponent<Collider>();
        colisor.isTrigger = true; 
        renderizador = GetComponentInChildren<Renderer>();
        posicaoInicial = transform.position;

        propBlock = new MaterialPropertyBlock();
        if (renderizador != null)
        {
            renderizador.GetPropertyBlock(propBlock);
            DefinirAlpha(0f); 
        }
    }

    void Update()
    {
        if (coletada) return;

        if (jogador == null)
        {
            if (GameManager.Instancia != null && GameManager.Instancia.jogadorNaCena != null)
            {
                jogador = GameManager.Instancia.jogadorNaCena.transform;
            }
            if (jogador == null) return; 
        }

        if (!revelada)
        {
            float distanciaAoQuadrado = (transform.position - jogador.position).sqrMagnitude;
            if (distanciaAoQuadrado <= (raioRevelacao * raioRevelacao))
            {
                revelada = true;
            }
        }
        else
        {
            ExecutarFade();
            AnimarMoeda();
        }
    }

    private void ExecutarFade()
    {
        if (tempoFade < duracaoFade)
        {
            tempoFade += Time.deltaTime;
            float alpha = Mathf.Clamp01(tempoFade / duracaoFade);
            DefinirAlpha(alpha);
        }
    }

    private void AnimarMoeda()
    {
        transform.Rotate(Vector3.up * velocidadeRotacao * Time.deltaTime);
        float novoY = posicaoInicial.y + Mathf.Sin(Time.time * velocidadeFlutuacao) * alturaFlutuacao;
        transform.position = new Vector3(transform.position.x, novoY, transform.position.z);
    }

    private void DefinirAlpha(float alpha)
    {
        if (renderizador == null || renderizador.sharedMaterial == null) return;
        Color corBase = renderizador.sharedMaterial.color;
        corBase.a = alpha;
        propBlock.SetColor("_Color", corBase);     
        propBlock.SetColor("_BaseColor", corBase);  
        renderizador.SetPropertyBlock(propBlock);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (coletada || !revelada) return;

        if (other.CompareTag("Player"))
        {
            Coletar(other.gameObject);
        }
    }

    private void Coletar(GameObject objetoJogador)
    {
        coletada = true;
        colisor.enabled = false;

        if (GameManager.Instancia != null)
        {
            GameManager.Instancia.RegistrarColeta();
        }

        if (vfxColeta != null) Instantiate(vfxColeta, transform.position, Quaternion.identity);
        if (sfxColeta != null) AudioSource.PlayClipAtPoint(sfxColeta, transform.position);

        Destroy(gameObject);
    }
}