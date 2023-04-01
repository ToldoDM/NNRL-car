using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class CarController : MonoBehaviour
{
    private Vector3 _startPosition, _startRotation;
    private const int DistanceDivision = 10;
    private const float MaxRayDistance = 10f;

    [Header("CurrentStats")] public float currentSpeed;
    public float currentAcceleration;
    public float nnAcceleration;
    public float nnSpeed;
    public float timeSinceStart = 0f;
    public float overallFitness;

    [Header("CarStats")] public float maxSpeed = 11.4f;
    public float acceleration = 5f;
    public float deceleration = 2f;
    public float turnSpeed = 0.02f;

    [Header("Fitness")] public float distanceMultiplier = 1.4f;
    public float avgSpeedMultiplier = 0.2f;
    public float sensorMultiplier = 0.1f;

    private Vector3 _lastPosition;
    private float _lastSpeed = 0f;
    private float _totalDistanceTravelled;
    private float _totalDistanceForward;
    private float _avgSpeed;

    private float _aSensor, _bSensor, _cSensor, _dSensor, _eSensor;

    private void Awake()
    {
        var transform1 = transform;
        _startPosition = transform1.position;
        _startRotation = transform1.eulerAngles;
    }

    public void Reset()
    {
        var transform1 = transform;
        currentSpeed = 0f;
        currentAcceleration = 0f;
        timeSinceStart = 0f;
        _totalDistanceTravelled = 0f;
        _avgSpeed = 0f;
        _lastPosition = _startPosition;
        _lastSpeed = 0f;
        overallFitness = 0f;
        transform1.position = _startPosition;
        transform1.eulerAngles = _startRotation;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.name.Equals("Ground"))
        {
            Reset();
        }
    }

    private void FixedUpdate()
    {
        InputSensors();
        _lastPosition = transform.position;

        //Neural network code here

        MoveCar();
        _lastSpeed = currentSpeed;

        timeSinceStart += Time.deltaTime;

        CalculateFitness();
    }


    private void CalculateFitness()
    {
        var currentDistance = Vector3.Distance(transform.position, _lastPosition);
        _totalDistanceForward = (currentSpeed >= 0)
            ? _totalDistanceForward + currentDistance
            : _totalDistanceForward - currentDistance;
        _totalDistanceTravelled += currentDistance;
        _avgSpeed = _totalDistanceTravelled / timeSinceStart;

        overallFitness = (_totalDistanceForward * distanceMultiplier) + (_avgSpeed * avgSpeedMultiplier) +
                         (((_aSensor + _bSensor + _cSensor + _dSensor + _eSensor) / 5) * sensorMultiplier);

        if (timeSinceStart > 20 && overallFitness < 40)
        {
            Reset();
        }

        if (overallFitness >= 1000)
        {
            //Saves network to a JSON
            Reset();
        }
    }

    private void InputSensors()
    {
        var transform1 = transform;
        var forward = transform1.forward;
        var right = transform1.right;
        var ninetyDeg = new Vector3(90, 0);
        Vector3 rayTop = (forward);
        Vector3 rayTopRight = (forward + right);
        Vector3 rayTopLeft = (forward - right);
        Vector3 rayRight = (forward + ninetyDeg);
        Vector3 rayLeft = (forward - ninetyDeg);

        Ray r = new Ray(transform1.position, rayTop);
        _aSensor = RayCastRays(r);
        r.direction = rayTopRight;
        _bSensor = RayCastRays(r);
        r.direction = rayTopLeft;
        _cSensor = RayCastRays(r);
        r.direction = rayRight;
        _dSensor = RayCastRays(r);
        r.direction = rayLeft;
        _eSensor = RayCastRays(r);

        // print("rayRight: " + aSensor);
        // print("rayTop: " + bSensor);
        // print("rayLeft: " + cSensor);
    }

    private float RayCastRays(Ray r)
    {
        RaycastHit hit;
        if (!Physics.Raycast(r, out hit, MaxRayDistance))
        {
            return 1;
        }

        Debug.DrawLine(r.origin, hit.point, Color.red);
        return hit.distance / DistanceDivision;
    }

    private void MoveCar()
    {
        var inputVertical = Input.GetAxis("Vertical");
        var inputHorizontal = Input.GetAxis("Horizontal");

        currentSpeed = inputVertical switch
        {
            > 0 => Mathf.MoveTowards(currentSpeed, maxSpeed, acceleration * Time.deltaTime),
            < 0 => Mathf.MoveTowards(currentSpeed, -maxSpeed / 2, acceleration * Time.deltaTime),
            _ => Mathf.MoveTowards(currentSpeed, 0, deceleration * Time.deltaTime)
        };
        nnSpeed = currentSpeed / maxSpeed;

        currentAcceleration = (currentSpeed - _lastSpeed) / Time.deltaTime;
        nnAcceleration = currentAcceleration / acceleration;

        transform.Translate(Vector3.forward * (currentSpeed * Time.deltaTime));
        if (currentSpeed != 0)
        {
            transform.eulerAngles += new Vector3(0, (inputHorizontal * 90) * turnSpeed, 0);
        }
    }
}