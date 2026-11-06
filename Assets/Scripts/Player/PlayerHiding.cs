using System.Collections.Generic;
using UnityEngine;

public class PlayerHiding : MonoBehaviour
{
    [Header("Configurações Visuais")]
    [Tooltip("Material Outline")]
    public Material materialOutline;

    public bool isHidden { get; private set; }

    private Renderer[] renderizadoresVisuais;
    private Dictionary<Renderer, Material[]> materiaisOriginais = new Dictionary<Renderer, Material[]>();

    void Awake()
    {
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

        foreach (var rend in renderizadoresVisuais)
        {
            Material[] matsOutline = new Material[rend.materials.Length];
            for (int i = 0; i < matsOutline.Length; i++)
            {
                matsOutline[i] = materialOutline;
            }
            rend.materials = matsOutline;
        }
    }

    public void SairEsconderijo()
    {
        if (!isHidden) return;
        isHidden = false;

        foreach (var rend in renderizadoresVisuais)
        {
            if (materiaisOriginais.ContainsKey(rend))
            {
                rend.materials = materiaisOriginais[rend];
            }
        }
    }
}