using UnityEngine;

public class SimpleFootIK : MonoBehaviour
{
    Animator animator;

    [Header("Setting")]
    [Range(0, 1f)]
    public float distanceToGround;
    public LayerMask layerMask;

    private Vector3 rightFootPos, leftFootPos, leftFootIkPos, rightFootIkPos;
    private Quaternion leftFootIkRot, rightFootIkRot;
    private float lastPelvisPositionY, lastRightFootPositionY, lastLeftFootPositionY;

    [Header("Smoothness")]
    public bool enableFeetIk = true;
    [Range(0, 2f)]
    public float heightFromGroundRaycast = 1.14f;
    [Range(0, 2f)]
    public float raycastDownDistance = 1.5f;
    [Range(0, 1f)]
    public float pelvisUpAndDownSpeed = 0.28f;
    [Range(0, 1f)]
    public float feetToIkPositionSpeed = 0.5f;

    public string leftFootAnimVariableName = "LeftFootCurve";
    public string rightFootAnimVariableName = "RightFootCurve";
    public bool useProIkFeature = false;
    public bool showSolverDebug = true;

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (!enableFeetIk || animator == null) return;

        MovePelvisHeight();

        // left foot
        animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 1);
        animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, animator.GetFloat(leftFootAnimVariableName));
        MoveFeetToIkPoint(AvatarIKGoal.LeftFoot, ref leftFootIkPos, ref leftFootIkRot, ref lastLeftFootPositionY);

        // right foot
        animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 1);
        animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, animator.GetFloat(rightFootAnimVariableName));
        MoveFeetToIkPoint(AvatarIKGoal.RightFoot, ref rightFootIkPos, ref rightFootIkRot, ref lastRightFootPositionY);
    }

    void MoveFeetToIkPoint(AvatarIKGoal foot, ref Vector3 ikPos, ref Quaternion ikRot, ref float lastFootPosY)
    {
        Vector3 targetIkPos = animator.GetIKPosition(foot);

        // 1. heightFromGroundRaycast
        Vector3 rayStart = targetIkPos + Vector3.up * heightFromGroundRaycast;
        Vector3 rayEnd = rayStart + Vector3.down * (raycastDownDistance + heightFromGroundRaycast);

        RaycastHit hit;

        // 2. Emitting rays
        if (Physics.Raycast(rayStart, Vector3.down, out hit, raycastDownDistance + heightFromGroundRaycast, layerMask))
        {
            // hit
            targetIkPos = hit.point;
            targetIkPos.y += distanceToGround;

            Quaternion footRot = Quaternion.LookRotation(transform.forward, hit.normal);
            ikRot = Quaternion.Slerp(ikRot, footRot, Time.deltaTime * 10f);

            // green line means I hit the ground
            Debug.DrawLine(rayStart, hit.point, Color.green);
            // green ball at the hit point
            Debug.DrawRay(hit.point, Vector3.up * 0.1f, Color.green);
        }
        else
        {
            // missed
            // red line means I missed the ground
            Debug.DrawLine(rayStart, rayEnd, Color.red);
        }

        animator.SetIKPosition(foot, targetIkPos);
        animator.SetIKRotation(foot, ikRot);
    }

    void MovePelvisHeight()
    {
        if (rightFootIkPos == Vector3.zero || leftFootIkPos == Vector3.zero || lastPelvisPositionY == 0)
        {
            lastPelvisPositionY = animator.bodyPosition.y;
            return;
        }

        float lOffsetPosition = leftFootIkPos.y - transform.position.y;
        float rOffsetPosition = rightFootIkPos.y - transform.position.y;

        float totalOffset = Mathf.Min(lOffsetPosition, rOffsetPosition);
        Vector3 newPelvisPos = animator.bodyPosition + Vector3.up * totalOffset;

        newPelvisPos.y = Mathf.Lerp(lastPelvisPositionY, newPelvisPos.y, pelvisUpAndDownSpeed);
        animator.bodyPosition = newPelvisPos;
        lastPelvisPositionY = animator.bodyPosition.y;
    }
}