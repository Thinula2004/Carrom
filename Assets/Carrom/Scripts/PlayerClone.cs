using UnityEngine;

public class PlayerClone : MonoBehaviour
{
    public SpriteRenderer sprite;

    void Start()
    {
        //sprite = GetComponent<SpriteRenderer>();
    }
    void OnEnable()
    {
        sprite.color = new Color32(255, 255, 255, 255);
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.gameObject.CompareTag("White") || collision.gameObject.CompareTag("Black") || collision.gameObject.CompareTag("Red"))
        {
            sprite.color = new Color32(255, 255, 255, 50);
        }
    }
}
