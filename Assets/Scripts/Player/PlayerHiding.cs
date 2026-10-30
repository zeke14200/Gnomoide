using System.Collections.Generic;
using UnityEngine;

public class PlayerHiding : MonoBehaviour
{
    [Header("Configurações Visuais")]
    [Tooltip("Material que usa o shader Custom/OutlineOnly")]
    public Material materialOutline;

    public bool isHidden { get; private set; }

    // Gerenciamento de Materiais
    private Renderer[] renderizadoresVisuais;
    private Dictionary<Renderer, Material[]> materiaisOriginais = new Dictionary<Renderer, Material[]>();

    void Awake()
    {
        // Captura todas as malhas visuais do jogador (corpo, roupas, etc)
        renderizadoresVisuais = GetComponentsInChildren<Renderer>();
        foreach (var rend in renderizadoresVisuais)
        {
            materiaisOriginais[rend] = rend.materials;
        }
    }

    public void EntrarEsconderijo()
    {
        if (isHidden) return;
        isHidden = true;

        // Aplica o efeito visual de Outline (deixando o interior invisível)
        foreach (var rend in renderizadoresVisuais)
        {
            Material[] matsOutline = new Material[rend.materials.Length];
            for (int i = 0; i < matsOutline.Length; i++)
            {
                matsOutline[i] = materialOutline;
            }
            rend.materials = matsOutline;
        }

        Debug.Log("Jogador entrou na zona de stealth do móvel.");
    }

    public void SairEsconderijo()
    {
        if (!isHidden) return;
        isHidden = false;

        // Devolve as cores e texturas originais ao personagem
        foreach (var rend in renderizadoresVisuais)
        {
            if (materiaisOriginais.ContainsKey(rend))
            {
                rend.materials = materiaisOriginais[rend];
            }
        }

        Debug.Log("Jogador saiu completamente do móvel. Visibilidade restaurada.");
    }
}