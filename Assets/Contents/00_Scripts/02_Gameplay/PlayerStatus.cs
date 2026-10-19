using UnityEngine;

[System.Serializable]
public class PlayerStatus
{
    public int maxHP = 100;
    public int currentHP = 100;
    public int currentShield = 0;

    // 회복 로직 통합 (어디서 부르든 알아서 최대 체력을 넘지 않게 조절)
    public void Heal(int amount)
    {
        if (amount <= 0) return; //0 이하나 음수 회복 차단
        currentHP = Mathf.Min(maxHP, currentHP + amount);
    }

    // 데미지 로직 통합 (보호막부터 깎고, 남은 데미지만 체력에 반영)
    public void TakeDamage(int damage)
    {
        if (damage <= 0) return;
        int finalDamage = damage;
        if (currentShield > 0)
        {
            if (currentShield >= finalDamage)
            {
                currentShield -= finalDamage;
                finalDamage = 0;
            }
            else
            {
                finalDamage -= currentShield;
                currentShield = 0;
            }
        }
        currentHP = Mathf.Max(0, currentHP - finalDamage);
    }

    // 게임 재시작 용도
    public void ResetStatus(int initialMaxHP)
    {
        maxHP = initialMaxHP;
        currentHP = maxHP;
        currentShield = 0;
    }
}