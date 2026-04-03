using UnityEngine;

public class MonsterHP : MonoBehaviour
{
    [SerializeField] private int maxHP = 10;
    private int currentHP;

    private MonsterDissolve monsterDissolve;

    private void Awake()
    {
        currentHP = maxHP;
        monsterDissolve = GetComponent<MonsterDissolve>();
    }

    public void TakeDamage(int damage)
    {
        currentHP -= damage;

        if (currentHP <= 0)
        {
            currentHP = 0;

            if (monsterDissolve != null)
                monsterDissolve.KillMonster();
            else
                Destroy(gameObject);
        }
    }
}