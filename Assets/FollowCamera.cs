using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FollowCamera : MonoBehaviour
{
    public Transform currentTarget;
    public string rotationXAxisInput = "Mouse X";
    public float rotationXSensitivity = 3.0f;
    public Vector2 rotationXAngleRange = new Vector2(-1080.0f, 1800.0f);
    public string rotationYAxisInput = "Mouse Y";
    public float rotationYSensitivity = 3.0f;
    public Vector2 rotationYAngleRange = new Vector2(-40.0f, 80.0f);
    public float rotationSmoothMultiplier = 12.0f;
    public string zoomAxisInput = "Mouse ScrollWheel";
    public float zoomSmoothMultiplier = 10.0f;
    public float zoomDistanceSensitivity = 3.0f;
    public Vector2 zoomDistanceRange = new Vector2(2.5f, 3.75f);
    public float zoomHeightSensitivity = 3.0f;
    public Vector2 zoomHeightRange = new Vector2(1.4f, 2.1f);
    public float zoomRightOffsetSensitivity = 3.0f;
    public Vector2 zoomRightOffsetRange = new Vector2(0.0f, 0.0f);
    public LayerMask cullingLayerMask = 1 << 0;

    private float defaultDistance;
    private float defaultHeight;
    private float defaultRightOffset;
    private float distance;
    private float height;
    private float currentHeight;
    private float rightOffset;

    private Camera _camera;
    private Transform lookAt;
    private Transform _target = null;
    private float rotationY = 0.0f;
    private float rotationX = 0.0f;
    private float cullingDistance;
    private float checkHeightRadius = 0.4f;
    private float clipPlaneMargin = 0.0f;
    private float forward = -1.0f;
    private float cullingHeight = 0.2f;
    private float cullingMinDist = 0.1f;
    private Vector3 centerView = new Vector3(0.5f, 0.5f, 0.0f);

    private void Awake()
    {
        defaultDistance = 10;
        //defaultDistance = zoomDistanceRange.x;
        defaultHeight = zoomHeightRange.x;
        height = defaultHeight;
        currentHeight = height;
        distance = defaultDistance;
        defaultRightOffset = zoomRightOffsetRange.x;
        rotationY = 50f;


        _camera = GetComponent<Camera>();
        GameObject lookAtInstance = new GameObject("LookAt(" + name + ")");
        lookAtInstance.hideFlags = HideFlags.HideAndDontSave;
        lookAt = lookAtInstance.transform;

        Canvas canvasChild = GetComponentInChildren<Canvas>();
        if (canvasChild != null)
        {
            transform.SetParent(null);
        }

        //_camera.depthTextureMode = DepthTextureMode.Depth;
    }
    public void SetTarget(Transform target)
    {
        if (_target == null || target != _target)
        {
            if (target != null)
            {
                if (target != transform)
                {
                    if (!target.IsChildOf(transform))
                    {
                        if (!transform.IsChildOf(target))
                        {
                            Debug.Log("Updating Camera Controller " + name + " Target To " + target.name);
                            currentTarget = target;
                            _target = target;
                        }
                        else
                        {
                            Debug.LogWarning("Could Not Initialize " + name + " Camera Controller : Controller Cannot Be A Child Of The Target");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("Could Not Initialize " + name + " Camera Controller : Target Cannot Be A Child Of The Controller");
                    }
                }
                else
                {
                    Debug.LogWarning("Could Not Initialize " + name + " Camera Controller : Target Cannot Be The Controller");
                }
            }
            else
            {
                if (_target != null)
                {
                    Debug.Log("Uninitializing Camera Controller " + name + " Target");
                    _target = null;
                }
            }
        }
    }
    public Ray GetLineOfSight()
    {
        return _camera.ViewportPointToRay(centerView);
    }
    private void Update()
    {
        SetTarget(currentTarget);
        //Only update if the mouse button is hold down.
        if (Input.GetMouseButton(1))
        {
            rotationX = rotationX + Input.GetAxis(rotationXAxisInput) * rotationXSensitivity;
            rotationY = Mathf.Clamp(rotationY - Input.GetAxis(rotationYAxisInput) * rotationYSensitivity, rotationYAngleRange.x, rotationYAngleRange.y);
        }

        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            defaultDistance = Mathf.Clamp(defaultDistance - Input.GetAxis(zoomAxisInput) * zoomDistanceSensitivity, zoomDistanceRange.x, zoomDistanceRange.y);
            defaultHeight = Mathf.Clamp(defaultHeight - Input.GetAxis(zoomAxisInput) * zoomHeightSensitivity, zoomHeightRange.x, zoomHeightRange.y);
            defaultRightOffset = Mathf.Clamp(defaultRightOffset - Input.GetAxis(zoomAxisInput) * zoomRightOffsetSensitivity, zoomRightOffsetRange.x, zoomRightOffsetRange.y);
        }
        else
        {
            defaultDistance = Mathf.Clamp(defaultDistance, zoomDistanceRange.x, zoomDistanceRange.y);
            defaultHeight = Mathf.Clamp(defaultHeight, zoomHeightRange.x, zoomHeightRange.y);
            defaultRightOffset = Mathf.Clamp(defaultRightOffset, zoomRightOffsetRange.x, zoomRightOffsetRange.y);
        }
    }

    private void FixedUpdate()
    {
        if (_target != null)
        {
            distance = Mathf.Lerp(distance, defaultDistance, zoomSmoothMultiplier * Time.fixedDeltaTime);
            height = Mathf.Lerp(height, defaultHeight, zoomSmoothMultiplier * Time.fixedDeltaTime);
            rightOffset = Mathf.Lerp(rightOffset, defaultRightOffset, zoomSmoothMultiplier * Time.fixedDeltaTime);

            cullingDistance = Mathf.Lerp(cullingDistance, distance, Time.fixedDeltaTime);
            var camDir = (forward * lookAt.forward) + (rightOffset * lookAt.right);
            camDir = camDir.normalized;

            var targetPos = _target.position;
            var desired_cPos = targetPos + new Vector3(0, height, 0);
            var current_cPos = targetPos + new Vector3(0, currentHeight, 0);
            RaycastHit hitInfo;

            //Check if Height is not blocked 
            if (Physics.SphereCast(targetPos, checkHeightRadius, Vector3.up, out hitInfo, cullingHeight + 0.2f, cullingLayerMask))
            {
                var t = hitInfo.distance - 0.2f;
                t -= height;
                t /= (cullingHeight - height);
                cullingHeight = Mathf.Lerp(height, cullingHeight, Mathf.Clamp(t, 0.0f, 1.0f));
            }
            currentHeight = height;

            //Check if target position with culling height applied is not blocked
            //if (CullingRayCast(current_cPos, planePoints, out hitInfo, distance, cullingLayerMask, Color.cyan))
            //{
            //    distance = Mathf.Clamp(cullingDistance, 0.0f, defaultDistance);
            //}
            var lookPoint = current_cPos + lookAt.forward * 2f;
            lookPoint += (lookAt.right * Vector3.Dot(camDir * (distance), lookAt.right));
            lookAt.position = current_cPos;

            Quaternion newRot = Quaternion.Euler(rotationY, rotationX, 0);
            lookAt.rotation = Quaternion.Slerp(lookAt.rotation, newRot, rotationSmoothMultiplier * Time.fixedDeltaTime);
            transform.position = current_cPos + (camDir * (distance));
            var cameraRot = Quaternion.LookRotation((lookPoint) - transform.position);
            transform.rotation = cameraRot;
        }
    }
}
