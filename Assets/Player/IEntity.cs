using UnityEngine;

public interface IEntity
{
    bool IsDead();

    float GetHealth();

    //Adds any ammount of health, use negative if means to damage.
    void AddHealth(float _health);
}
