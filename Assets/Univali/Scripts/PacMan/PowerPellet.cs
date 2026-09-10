using UnityEngine;

public class PowerPellet : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Pacman"))
        {
            // Notifica o sistema que a pílula foi comida
            GameEvents.TriggerPowerPelletEaten();

            // Destrói o objeto da pílula
            Destroy(gameObject);
        }
    }
}