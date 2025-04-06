using UnityEngine;

public class PlayerAnimations : MonoBehaviour
{
    [SerializeField]
    private Animator animator;
    [SerializeField]
    private PlayerMovement playerMovement;

    private void Update()
    {
        animator.SetFloat("X Speed", playerMovement.LocalDirection.x);
        animator.SetFloat("Y Speed", playerMovement.LocalDirection.y);
        animator.SetBool("Grounded", playerMovement.IsGrounded);
        animator.SetBool("Dashing", playerMovement.IsDashing);
    }
}
