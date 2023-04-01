using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(NNet))]
public class CarController : MonoBehaviour
{
    private Vector3 _startPosition, _startRotation;
    private NNet _network;
    private const int DistanceDivision = 10;
    private const float MaxRayDistance = 10f;
    public float nnAcceleration;
    public float nnSpeed;

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
    public float aSensor;
    public float bSensor;
    public float cSensor;
    public float dSensor;
    public float eSensor;
    private List<float> _nnInputs;

    private void Awake()
    {
        var transform1 = transform;
        _startPosition = transform1.position;
        _startRotation = transform1.eulerAngles;
        _nnInputs = new List<float>();
        _network = GetComponent<NNet>();
        _network.Initialise(layers, neurons, 7, 2);
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
        _network.Initialise(layers, neurons, 7, 2);
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
        // var a = Input.GetAxis("Vertical");
        // var s = Input.GetAxis("Horizontal");
        // MoveCar(a, s);

        //Neural network
        ClearAndAddInputs();
        var nnOut = _network.RunNetwork(_nnInputs);
        MoveCar(nnOut[0], nnOut[1]);

        _lastSpeed = currentSpeed;
        timeSinceStart += Time.deltaTime;

        CalculateFitness();
    }

    private void ClearAndAddInputs()
    {
        _nnInputs.Clear();
        _nnInputs.Add(aSensor);
        _nnInputs.Add(bSensor);
        _nnInputs.Add(cSensor);
        _nnInputs.Add(dSensor);
        _nnInputs.Add(eSensor);
        _nnInputs.Add(nnAcceleration);
        _nnInputs.Add(nnSpeed);
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
                         (((aSensor + bSensor + cSensor + dSensor + eSensor) / 5) * sensorMultiplier);

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
        Vector3 rayTop = (forward);
        Vector3 rayTopRight = (forward + right);
        Vector3 rayTopLeft = (forward - right);
        Vector3 rayRight = Quaternion.Euler(0, 90, 0) * forward;
        Vector3 rayLeft = Quaternion.Euler(0, -90, 0) * forward;

        var r = new Ray(transform1.position, rayTop);
        aSensor = RayCastRays(r);
        r.direction = rayTopRight;
        bSensor = RayCastRays(r);
        r.direction = rayTopLeft;
        cSensor = RayCastRays(r);
        r.direction = rayRight;
        dSensor = RayCastRays(r);
        r.direction = rayLeft;
        eSensor = RayCastRays(r);

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

    public void MoveCar(float inputVertical, float inputHorizontal)
    {
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