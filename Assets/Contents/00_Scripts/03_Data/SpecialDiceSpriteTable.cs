using UnityEngine;

// 홀수·짝수 전용 면 스프라이트. Resources/SpecialDiceSpriteTable 로 로드한다.
[CreateAssetMenu(fileName = "SpecialDiceSpriteTable", menuName = "Dice/Special Dice Sprite Table")]
public class SpecialDiceSpriteTable : ScriptableObject
{
    [Tooltip("면 번호와 눈-1 모두 같은 결과가 나오도록 1,1,3,3,5,5 순")]
    public Sprite[] oddFaces = new Sprite[6];
    [Tooltip("면 번호와 눈-1 모두 같은 결과가 나오도록 2,2,4,4,6,6 순")]
    public Sprite[] evenFaces = new Sprite[6];
}
