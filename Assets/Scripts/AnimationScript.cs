using UnityEngine;
using System.Collections;

public class AnimationScript : MonoBehaviour {

    [SerializeField] Sprite[] sprites;
    SpriteRenderer image;
    int count = 0;
    float timePerFrame = 0.03F;
    float cooldown = 0f;

    void Start () {
        image = GetComponent<SpriteRenderer>();
    }
	
	void Update () {
        cooldown += Time.deltaTime;
        if (timePerFrame < cooldown)
        {
            cooldown = 0;
            if (count > 27)
                count = 0;
            image.sprite = sprites[count++];
        }
    }
}
