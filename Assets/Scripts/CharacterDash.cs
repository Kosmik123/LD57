using NaughtyAttributes;
using System;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class CharacterDash : MonoBehaviour
{
    [SerializeField]
    private KeyCode dashKey = KeyCode.Space;

    [SerializeField]
    private float dashSpeed = 5f;
    [SerializeField]
    private float dashDuration = 0.5f;

    [SerializeField]
    private Behaviour[] componentsDisabledWhileDashing;

    [ShowNonSerializedField]
    private bool isDashing;
    [ShowNonSerializedField]
    private Vector3 velocity;

    private CharacterController characterController;

    private Vector3 dashDirection;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (isDashing)
        {
            characterController.Move(dashSpeed * Time.deltaTime * dashDirection);
        }
        else if (Input.GetKeyDown(dashKey))
        {
            Dash();
        }
    }

    private void Dash()
    {
        isDashing = true;
        foreach (var c in componentsDisabledWhileDashing)
            c.enabled = false;

        velocity = characterController.velocity;
        velocity.y = 0;
        if (velocity.sqrMagnitude > 0.001f)
        {
            dashDirection = velocity.normalized;
        }
        else
        {
            dashDirection = transform.forward;
        }
        Invoke(nameof(EndDash), dashDuration);
    }

    private void EndDash()
    {
        foreach (var c in componentsDisabledWhileDashing)
            c.enabled = true;
        isDashing = false;
    }
}
