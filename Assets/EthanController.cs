using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This class is hardly based on the CharacterController script in the Standard Assets.
/// The main logic that allow to the character stick to the ground is inside the Move method.
/// If you want to implement such behaviour in you own code just use a CharacterController component
/// in you game object and try to reproduce the logic in this method in you own code.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class EthanController : MonoBehaviour
{
    /// <summary>
    /// Gravity used when the character is airbone.
    /// </summary>
    [Tooltip("Gravity used when the character is airbone.")]
    [SerializeField]
    float m_Gravity = 20.0F;
    /// <summary>
    /// Factor used as jump impulse.
    /// </summary>
    [Tooltip("Factor used as jump impulse.")]
    [SerializeField]
    float m_JumpPower = 12f;
    /// <summary>
    /// The translation speed.
    /// </summary>
    [Tooltip("The translation speed.")]
    [SerializeField]
    float m_speed = 5f;
    /// <summary>
    /// Factor used to stick the character to the ground.
    /// </summary>
    [Tooltip("Factor used to stick the character to the ground.")]
    [SerializeField]
    float stickToGroundFactor = 400;

    float m_TurnAmount;
    float m_ForwardAmount;
    CharacterController m_controller;
    Animator m_Animator;
    bool m_Jump;
    bool m_IsGrounded;
    Transform m_Cam;
    Vector3 m_CamForward;
    Vector3 m_Move;
    Vector3 moveDirection;
    bool m_jumping;
    float m_MovingTurnSpeed = 360;
    float m_StationaryTurnSpeed = 180;

    #region Monobehaviour
    void Start()
    {
        m_Animator = GetComponent<Animator>();
        m_controller = GetComponent<CharacterController>();

        // get the transform of the main camera
        if (Camera.main != null)
        {
            m_Cam = Camera.main.transform;
        }
        else
        {
            Debug.LogWarning(
                "Warning: no main camera found. Third person character needs a Camera tagged \"MainCamera\", for camera-relative controls.", gameObject);
            // we use self-relative controls in this case, which probably isn't what the user wants, but hey, we warned them!
        }
    }

    void Update()
    {
        if (!m_Jump)
        {
            m_Jump = Input.GetButtonDown("Jump");
        }
    }

    private void FixedUpdate()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        // calculate move direction to pass to character
        if (m_Cam != null)
        {
            // calculate camera relative direction to move:
            m_CamForward = Vector3.Scale(m_Cam.forward, new Vector3(1, 0, 1)).normalized;

            m_Move = v * m_CamForward + h * m_Cam.right;
        }
        else
        {
            // we use world-relative directions in the case of no main camera
            m_Move = v * Vector3.forward + h * Vector3.right;
        }

        Move(m_Move, m_Jump);
    }

    public void OnAnimatorMove()
    {
        // we implement this function to override the default root motion.
        // this allows us to modify the positional speed before it's applied.
        if (m_IsGrounded && Time.deltaTime > 0)
        {
            Vector3 v = m_Animator.deltaPosition / Time.deltaTime;

            v.y = m_controller.velocity.y;
        }
    }
    #endregion

    /// <summary>
    /// This method perform the translation of the character
    /// using the moving vector.
    /// </summary>
    /// <param name="move">moving vector</param>
    /// <param name="jump">flag for perform a jump</param>
    public void Move(Vector3 move, bool jump)
    {
        if (move.magnitude > 1f) move.Normalize();
        move = transform.InverseTransformDirection(move);
        // Check if we are on the ground
        CheckGroundStatus();

        move = Vector3.ProjectOnPlane(move, Vector3.up);
        m_TurnAmount = Mathf.Atan2(move.x, move.z);
        m_ForwardAmount = move.z;

        ApplyExtraTurnRotation();

        if (m_IsGrounded)
        {
            if (m_jumping) m_jumping = false;

            // Calculate the current move direction
            moveDirection = move;
            moveDirection = transform.TransformDirection(moveDirection);
            moveDirection *= m_speed * Mathf.InverseLerp(180, 0, m_TurnAmount * Mathf.Rad2Deg);

            if (Input.GetButton("Jump"))
            {
                m_jumping = true;
                moveDirection.y = m_JumpPower;
            }
        }

        // If we are jumping or falling, just apply the normal gravity
        // But if we are on the ground, apply the extra gravity to stick the character to the ground
        if (m_jumping)
            moveDirection.y -= m_Gravity * Time.deltaTime;
        else if (!m_IsGrounded)
            moveDirection.y -= m_Gravity * Time.deltaTime;
        else
            moveDirection.y -= stickToGroundFactor * Time.deltaTime;

        // Move the character using the Character Controller
        m_controller.Move(moveDirection * Time.deltaTime);

        // send input and other state parameters to the animator
        UpdateAnimator(move);
    }

    void ApplyExtraTurnRotation()
    {
        // help the character turn faster (this is in addition to root rotation in the animation)
        float turnSpeed = Mathf.Lerp(m_StationaryTurnSpeed, m_MovingTurnSpeed, m_ForwardAmount);
        transform.Rotate(0, m_TurnAmount * turnSpeed * Time.deltaTime, 0);
    }

    void CheckGroundStatus()
    {
        if (m_controller.isGrounded)
        {
            m_IsGrounded = true;
            m_Animator.applyRootMotion = true;
        }
        else
        {
            m_IsGrounded = false;
            m_Animator.applyRootMotion = false;
        }
    }

    void UpdateAnimator(Vector3 move)
    {
        // update the animator parameters
        m_Animator.SetFloat("Forward", m_ForwardAmount, 0.1f, Time.deltaTime);
        m_Animator.SetFloat("Turn", m_TurnAmount, 0.1f, Time.deltaTime);
        m_Animator.SetBool("OnGround", m_IsGrounded);

        if (!m_IsGrounded)
        {
            m_Animator.SetFloat("Jump", m_controller.velocity.y);
        }

        // calculate which leg is behind, so as to leave that leg trailing in the jump animation
        // (This code is reliant on the specific run cycle offset in our animations,
        // and assumes one leg passes the other at the normalized clip times of 0.0 and 0.5)
        float runCycle =
            Mathf.Repeat(
                m_Animator.GetCurrentAnimatorStateInfo(0).normalizedTime + .2f, 1);
        float jumpLeg = (runCycle < .5f ? 1 : -1) * m_ForwardAmount;
        if (m_IsGrounded)
        {
            m_Animator.SetFloat("JumpLeg", jumpLeg);
        }
    }
}
