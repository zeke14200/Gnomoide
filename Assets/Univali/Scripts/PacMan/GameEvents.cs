using System;

public static class GameEvents
{
    // Evento disparado quando qualquer Power-Pellet é consumida
    public static event Action OnPowerPelletEaten;

    public static void TriggerPowerPelletEaten()
    {
        OnPowerPelletEaten?.Invoke();
    }
}