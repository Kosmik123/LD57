﻿using UnityEngine;

public class PlayerAnimations : MonoBehaviour
{
    [SerializeField]
    private Animator animator;
    [SerializeField]
    private PlayerController playerMovement;

    private void OnEnable()
    {
        playerMovement.OnAttack += PlayerMovement_OnAttack;
    }

    private void PlayerMovement_OnAttack()
    {
        animator.SetTrigger("Attack");
    }

    private void Update()
    {
        animator.SetFloat("X Speed", playerMovement.LocalDirection.x);
        animator.SetFloat("Y Speed", playerMovement.LocalDirection.y);
        animator.SetBool("Grounded", playerMovement.IsGrounded);
        animator.SetBool("Dashing", playerMovement.IsDashing);
    }

    private void OnDisable()
    {
        playerMovement.OnAttack -= PlayerMovement_OnAttack;
    }
}
