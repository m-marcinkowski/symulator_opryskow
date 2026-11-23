using UnityEngine;

public class CameraController : MonoBehaviour
{
    public float lookSpeed = 2.0f;
    public float moveSpeed = 5000.0f;
    public float zoomSpeed = 2000.0f;
    public float dragSpeed = 100.0f;

    private float rotationX = 0.0f;
    private float rotationY = 0.0f;
    private bool isBirdsEyeView = false; // Flaga dla trybu LOT_PTAKA

    // === FOLLOW / ORBIT ===
    bool _followEnabled;
    Transform _followTarget;
    float _followSmooth = 5f;          // wyg³adzanie pozycji/rotacji
    bool _lockUserInputWhileFollow = true;

    // sferyczne wspó³rzêdne kamery wzglêdem traktora (w lokalnym uk³adzie targetu)
    float _yawDeg;                     // obrót wokó³ osi Y targetu (lewo/prawo)
    float _pitchDeg;                   // k¹t wzniesienia (góra/dó³)
    float _distance;                   // odleg³oœæ kamery od targetu

    [Header("Follow/Orbit settings")]
    public float minPitchDeg = 10f;    // nie patrz z p³aszczyzny
    public float maxPitchDeg = 80f;
    public float minDistance = 6f;
    public float maxDistance = 80f;
    public float orbitYawSpeed = 120f;     // deg/s przy PPM
    public float orbitPitchSpeed = 120f;   // deg/s przy PPM
    public float orbitZoomSpeed = 40f;     // m/s przy scrollu
    public float minWorldY = 0.5f;         // nigdy poni¿ej tego Y (bezpiecznik)

    void Update()
    {
        if (_followEnabled)
        {
            HandleFollowOrbit();
            return;
        }

        if (isBirdsEyeView) HandleBirdsEyeView();
        else HandleStandardView();
    }

    private void HandleStandardView()
    {
        // Rotate camera with right mouse button
        if (Input.GetMouseButton(1))
        {
            rotationX += Input.GetAxis("Mouse X") * lookSpeed;
            rotationY -= Input.GetAxis("Mouse Y") * lookSpeed;
            transform.eulerAngles = new Vector3(rotationY, rotationX, 0.0f);
        }

        // Move camera with WASD keys
        float moveX = Input.GetAxis("Horizontal") * moveSpeed * Time.deltaTime;
        float moveZ = Input.GetAxis("Vertical") * moveSpeed * Time.deltaTime;
        transform.Translate(moveX, 0.0f, moveZ);

        // Zoom camera with mouse scroll
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        transform.Translate(0.0f, 0.0f, scroll * zoomSpeed, Space.Self);

        // Drag camera with middle mouse button
        if (Input.GetMouseButton(2))
        {
            float dragX = -Input.GetAxis("Mouse X") * dragSpeed;
            float dragY = -Input.GetAxis("Mouse Y") * dragSpeed;
            transform.Translate(dragX, dragY, 0.0f);
        }
    }

    private void HandleBirdsEyeView()
    {
        // Disable camera rotation
        rotationX = 0.0f;
        rotationY = 0.0f;
        transform.rotation = Quaternion.Euler(90, 0, 0); // Fixed top-down view

        // Move camera with WASD keys in XZ plane
        float moveX = Input.GetAxis("Horizontal") * moveSpeed * Time.deltaTime;
        float moveZ = Input.GetAxis("Vertical") * moveSpeed * Time.deltaTime;
        transform.Translate(moveX, 0.0f, moveZ, Space.World);

        // Zoom camera with mouse scroll (only in Y axis)
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        transform.Translate(0.0f, -scroll * zoomSpeed, 0.0f, Space.World);

        // Drag camera with middle mouse button
        if (Input.GetMouseButton(2))
        {
            float dragX = -Input.GetAxis("Mouse X") * dragSpeed;
            float dragZ = -Input.GetAxis("Mouse Y") * dragSpeed;
            transform.Translate(dragX, 0.0f, dragZ, Space.World);
        }
    }

    public void SetBirdsEyeView(bool enable)
    {
        isBirdsEyeView = enable;
        if (enable)
        {
            transform.position = new Vector3(transform.position.x, 100, transform.position.z);
            transform.rotation = Quaternion.Euler(90, 0, 0);
            _followEnabled = false;
        }
    }

    public void StartFollow(Transform target, Vector3 offsetRelativeToTarget, float smooth = 5f, bool lockUserInput = true)
    {
        _followTarget = target;
        _followSmooth = Mathf.Max(0.1f, smooth);
        _lockUserInputWhileFollow = lockUserInput;

        if (offsetRelativeToTarget.sqrMagnitude < 0.0001f)
            offsetRelativeToTarget = new Vector3(0f, 12f, -18f);

        // Zamieniamy pocz¹tkowy offset na k¹ty sferyczne i dystans w lokalnym uk³adzie targetu
        Vector3 local = _followTarget.InverseTransformVector(offsetRelativeToTarget);
        _distance = Mathf.Clamp(local.magnitude, minDistance, maxDistance);

        // yaw: k¹t w p³aszczyŸnie XZ (oœ Y targetu), pitch: wzniesienie
        _yawDeg = Mathf.Atan2(local.x, -local.z) * Mathf.Rad2Deg; // -Z to "za pojazdem"
        _pitchDeg = Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
        _pitchDeg = Mathf.Clamp(_pitchDeg, minPitchDeg, maxPitchDeg);

        isBirdsEyeView = false;
        _followEnabled = true;
    }

    public void StopFollow()
    {
        _followEnabled = false;
        _followTarget = null;
    }

    void HandleFollowOrbit()
    {
        if (_followTarget == null) { _followEnabled = false; return; }

        // Sterowanie tylko PPM (obrót) i rolk¹ (zoom)
        if (Input.GetMouseButton(1))
        {
            _yawDeg += Input.GetAxis("Mouse X") * orbitYawSpeed * Time.deltaTime;
            _pitchDeg -= Input.GetAxis("Mouse Y") * orbitPitchSpeed * Time.deltaTime;
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f)
            _distance = Mathf.Clamp(_distance - scroll * orbitZoomSpeed, minDistance, maxDistance);

        _pitchDeg = Mathf.Clamp(_pitchDeg, minPitchDeg, maxPitchDeg);

        // Liczymy offset w LOKALNYM uk³adzie targetu, ¿eby kamera „jecha³a za” nim
        Vector3 localOffset = Quaternion.Euler(_pitchDeg, _yawDeg, 0f) * new Vector3(0, 0, -_distance);
        Vector3 desiredPos = _followTarget.TransformPoint(localOffset);

        // Nigdy poni¿ej p³aszczyzny (bezpiecznik)
        if (desiredPos.y < minWorldY) desiredPos.y = minWorldY;

        // P³ynne dojœcie i patrzenie w punkt nad pojazdem
        transform.position = Vector3.Lerp(transform.position, desiredPos, _followSmooth * Time.deltaTime);

        Vector3 lookPoint = _followTarget.position + Vector3.up * 0.5f;
        Quaternion desiredRot = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, _followSmooth * Time.deltaTime);
    }
}
