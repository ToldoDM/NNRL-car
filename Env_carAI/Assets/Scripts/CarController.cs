using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class CarController : MonoBehaviour
{
    private Vector3 _startPosition, _startRotation;
    private NNet _network;
    private const int DistanceDivision = 10;
    private const float MaxRayDistance = 10f;
    private float _nnAcceleration;
    private float _nnSpeed;

    [Header("CurrentStats")] public float currentSpeed;
    public float currentAcceleration;
    public float timeSinceStart = 0f;
    public float overallFitness;
    public int currentRun = 1;

    [Header("Network Options")] public int layers = 1;
    public int neurons = 10;

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
    private float _aSensor;
    private float _bSensor;
    private float _cSensor;
    private float _dSensor;

    private float _eSensor;
    // private List<float> _nnInputs;

    private void Awake()
    {
        var transform1 = transform;
        _startPosition = transform1.position;
        _startRotation = transform1.eulerAngles;
        // _nnInputs = new List<float>();
        // _network = GetComponent<NNet>();
        // _network.Initialise(layers, neurons, 7, 2);
    }

    public void Reset()
    {
        currentRun++;
        var transform1 = transform;
        currentSpeed = 0f;
        currentAcceleration = 0f;
        timeSinceStart = 0f;
        _lastPosition = _startPosition;
        _lastSpeed = 0f;
        transform1.position = _startPosition;
        transform1.eulerAngles = _startRotation;
        _totalDistanceTravelled = 0f;
        _totalDistanceForward = 0f;
        _avgSpeed = 0f;
        overallFitness = 0f;
        // _network.Initialise(layers, neurons, 7, 2);
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

        //Human controlled
        var a = Input.GetAxis("Vertical");
        var s = Input.GetAxis("Horizontal");
        MoveCar(a, s);

        //Neural network
        // ClearAndAddInputs();
        // var nnOut = _network.RunNetwork(_nnInputs);
        // MoveCar(nnOut[0], nnOut[1]);

        _lastSpeed = currentSpeed;
        timeSinceStart += Time.deltaTime;

        CalculateFitness();
    }

    // private void ClearAndAddInputs()
    // {
    //     _nnInputs.Clear();
    //     _nnInputs.Add(_aSensor);
    //     _nnInputs.Add(_bSensor);
    //     _nnInputs.Add(_cSensor);
    //     _nnInputs.Add(_dSensor);
    //     _nnInputs.Add(_eSensor);
    //     _nnInputs.Add(_nnAcceleration);
    //     _nnInputs.Add(_nnSpeed);
    // }


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
            Reset();
        }
    }

    private void InputSensors()
    {
        var transform1 = transform;
        var forward = transform1.forward;
        var right = transform1.right;
        Vector3 rayTop = (forward);
        Vector3 rayTopRight = (forward + right);
        Vector3 rayTopLeft = (forward - right);
        Vector3 rayRight = Quaternion.Euler(0, 90, 0) * forward;
        Vector3 rayLeft = Quaternion.Euler(0, -90, 0) * forward;

        var r = new Ray(transform1.position, rayTop);
        _aSensor = RayCastRays(r);
        r.direction = rayTopRight;
        _bSensor = RayCastRays(r);
        r.direction = rayTopLeft;
        _cSensor = RayCastRays(r);
        r.direction = rayRight;
        _dSensor = RayCastRays(r);
        r.direction = rayLeft;
        _eSensor = RayCastRays(r);
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

    private void MoveCar(float inputVertical, float inputHorizontal)
    {
        var isGoingInt = Convert.ToInt32(currentSpeed >= 0);
        currentSpeed = inputVertical switch
        {
            > 0 => Mathf.MoveTowards(currentSpeed, maxSpeed,
                acceleration * (2 - isGoingInt) * Time.deltaTime),
            < 0 => Mathf.MoveTowards(currentSpeed, -maxSpeed / 2,
                acceleration * (isGoingInt + 1) * Time.deltaTime),
            _ => Mathf.MoveTowards(currentSpeed, 0, deceleration * Time.deltaTime)
        };
        _nnSpeed = currentSpeed / maxSpeed;

        currentAcceleration = (currentSpeed - _lastSpeed) / Time.deltaTime;
        _nnAcceleration = currentAcceleration / acceleration * 2;

        transform.Translate(Vector3.forward * (currentSpeed * Time.deltaTime));
        if (currentSpeed != 0)
        {
            transform.eulerAngles += new Vector3(0, (inputHorizontal * 90) * turnSpeed, 0);
        }
    }
}