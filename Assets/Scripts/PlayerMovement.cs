using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;

    private Rigidbody2D rb;
    private Animator anim;
    private SpriteRenderer sr;

    // Remember the last direction
    private float lastMoveX = 1f;   // default facing right
    private float lastMoveY = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");

        // ----- Prevent diagonal movement -----
        if (moveX != 0 && moveY != 0)
        {
            // If both are pressed, keep only the stronger one
            if (Mathf.Abs(moveX) > Mathf.Abs(moveY))
                moveY = 0;
            else
                moveX = 0;
        }
        // -------------------------------------

        Vector2 movement = new Vector2(moveX, moveY).normalized;
        rb.linearVelocity = movement * moveSpeed;

        if (movement.magnitude > 0.1f)
        {
            // We are moving → save the direction
            lastMoveX = moveX;
            lastMoveY = moveY;

            // Choose Walk animation
            if (Mathf.Abs(moveX) > Mathf.Abs(moveY))
            {
                // Left or Right
                anim.Play("Side_Walk");
                sr.flipX = moveX < 0;
            }
            else
            {
                // Up or Down
                if (moveY > 0)
                    anim.Play("Back_Walk");
                else
                    anim.Play("Front_Walk");

                sr.flipX = false;
            }
        }
        else
        {
            // Standing still = play Idle facing the last direction
            if (Mathf.Abs(lastMoveX) > Mathf.Abs(lastMoveY))
            {
                anim.Play("Side_Idle");
                sr.flipX = lastMoveX < 0;
            }
            else
            {
                if (lastMoveY > 0)
                    anim.Play("Back_Idle");
                else
                    anim.Play("Front_Idle");

                sr.flipX = false;
            }
        }
    }
}