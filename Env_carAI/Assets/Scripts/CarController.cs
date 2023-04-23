using System;
using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

[RequireComponent(typeof(NNet))]
public class CarController : MonoBehaviour
{
    [SerializeField] private List<Transform> spawnPoints;
    [SerializeField] private List<Transform> checkpoints;

    [Header("CurrentStats")] public bool humanControlled = false;
    public float currentSpeed;
    public float currentAcceleration;
    public float timeSinceLastCheckpoint = 0f;
    public float distanceTraveledSinceCheckpoint = 0f;
    public float distanceToNextGate = -1f;
    public float distanceToPrevGate = -1f;
    public float overallFitness = 0f;

    [Header("CarStats")] public float maxSpeed = 11.4f;
    public float acceleration = 5f;
    public float deceleration = 2f;
    public float turnSpeed = 0.02f;

    [Header("Fitness")] public float currentReward = 0f;
    public float distanceMultiplier = 1.4f;
    public float speedMultiplier = 0.2f;
    public float gateMultiplier = 1f;
    public int giveReward = 0;
    
    [Header("Neural Network")]
    public int inputLayer = 12;
    public int outputLayer = 2;

    [HideInInspector] public NNet network;

    private Vector3 _startPosition, _startRotation;
    private const int DistanceDivision = 10;
    private const float MaxRayDistance = 10f;
    private float _nnAcceleration;
    private float _nnSpeed;
    private List<float> _nnInputs = new List<float>();
    private Vector3 _lastPosition;
    private float _lastSpeed = 0f;
    private int _lastCheckpoint = -1;
    private string _spawnName = "";
    private Vector3 _nextGatePosition = Vector3.zero;
    private Vector3 _prevGatePosition = Vector3.zero;
    private float _prevDistanceToNextGate = 0f;
    private RayPerceptionSensorComponent3D _raySensor;
    private const float CheckpointTimeout = 10f;
    private GeneticManager _manager;
    private bool _dead = false;

    private void Awake()
    {
        network = gameObject.GetComponent<NNet>();
        _manager = GameObject.FindObjectOfType<GeneticManager>();
        _raySensor = gameObject.GetComponent<RayPerceptionSensorComponent3D>();
        network.Initialise(inputLayer, outputLayer);
        _manager.AddCar(this);
        Reset();
    }

    public void Reset()
    {
        currentSpeed = 0f;
        currentAcceleration = 0f;
        _dead = false;
        _lastSpeed = 0f;
        _lastCheckpoint = -1;
        timeSinceLastCheckpoint = 0f;
        distanceTraveledSinceCheckpoint = 0f;
        giveReward = 0;
        _spawnName = "";
        distanceToNextGate = -1f;
        distanceToPrevGate = -1f;
        _nextGatePosition = Vector3.zero;
        _prevGatePosition = Vector3.zero;
        overallFitness = 0f;
        GetRandomPosition();
    }

    private void Death()
    {
        currentSpeed = 0f;
        currentAcceleration = 0f;
        _dead = true;
        _manager.Death();
    }

    private void GetRandomPosition()
    {
        var carObject = transform;
        var spawnTransform = spawnPoints[Random.Range(0, spawnPoints.Count)];
        var position = spawnTransform.position;
        var eulerAngles = spawnTransform.eulerAngles;
        carObject.position = position;
        carObject.eulerAngles = eulerAngles;
        _lastPosition = position;
        _startRotation = eulerAngles;
        CalculateNextGate();
    }

    private void CalculateNextGate()
    {
        var closestGate = 0;
        var minDistance = 10000f;
        foreach (var cpt in checkpoints)
        {
            if (!(Vector3.Distance(transform.position, cpt.position) < minDistance)) continue;
            minDistance = Vector3.Distance(transform.position, cpt.position);
            closestGate = checkpoints.IndexOf(cpt);
        }

        GivePrevNextGate(closestGate);
    }

    private void GivePrevNextGate(int index)
    {
        _lastCheckpoint = index;
        timeSinceLastCheckpoint = 0f;
        distanceTraveledSinceCheckpoint = 0f;
        _nextGatePosition = checkpoints[(_lastCheckpoint + 1) % checkpoints.Count].position;
        _prevGatePosition = checkpoints[(_lastCheckpoint + checkpoints.Count - 1) % checkpoints.Count].position;
        _prevDistanceToNextGate = Vector3.Distance(transform.position, _nextGatePosition);
    }

    private void HandleCheckpoint(Transform checkpoint)
    {
        var index = checkpoints.IndexOf(checkpoint);

        if (index != (_lastCheckpoint + 1) % checkpoints.Count)
        {
            var exponent = 0;
            for (var i = index; i % checkpoints.Count != _lastCheckpoint; i++)
            {
                exponent++;
            }

            // AddReward((float)-Math.Pow(2, exponent) + 1);
            overallFitness += (float)-Math.Pow(2, exponent) + 1;
        }
        else
        {
            var reward = distanceTraveledSinceCheckpoint / timeSinceLastCheckpoint;
            // AddReward(reward < 1
            //     ? 1 * gateMultiplier
            //     : reward * gateMultiplier);
            overallFitness += reward < 1
                ? 1 * gateMultiplier
                : reward * gateMultiplier;
            GivePrevNextGate(index);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        switch (other.tag)
        {
            case "Spawn":
                if (other.name == _spawnName)
                {
                    giveReward = 1;
                }

                break;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        switch (other.tag)
        {
            case "checkpoints":
                HandleCheckpoint(other.gameObject.transform);
                break;
            case "Spawn":
                if (_spawnName == "")
                {
                    _spawnName = other.name;
                    giveReward = 0;
                }

                break;
            default:
                // AddReward(-10f);
                overallFitness += -10f;
                Death();
                // Reset();
                break;
        }
    }

    private void FixedUpdate()
    {
        if (_dead) return;

        if (humanControlled)
        {
            //Human controlled
            var a = Input.GetAxis("Vertical");
            var s = Input.GetAxis("Horizontal");
            MoveCar(a, s);
        }
        else
        {
            //Neural network
            ClearAndAddInputs();
            var nnOut = network.RunNetwork(_nnInputs);
            MoveCar(nnOut[0], nnOut[1]);
        }

        currentReward = CalculateReward();

        timeSinceLastCheckpoint += Time.deltaTime;
        var position = transform.position;
        distanceToNextGate = Vector3.Distance(position, _nextGatePosition);
        distanceToPrevGate = Vector3.Distance(position, _prevGatePosition);
        Debug.DrawLine(position, _nextGatePosition, Color.magenta);
        Debug.DrawLine(position, _prevGatePosition, Color.blue);
        overallFitness += ((_prevDistanceToNextGate - distanceToNextGate));
        _prevDistanceToNextGate = distanceToNextGate;

        if (timeSinceLastCheckpoint <= CheckpointTimeout && overallFitness < 3000) return;
        Death();
        // Reset();
    }

    private void ClearAndAddInputs()
    {
        var raysOut = RayPerceptionSensor.Perceive(_raySensor.GetRayPerceptionInput()).RayOutputs;
        // Add observations
        _nnInputs.Clear();
        _nnInputs.Add(raysOut[0].HitFraction);
        _nnInputs.Add(raysOut[1].HitFraction);
        _nnInputs.Add(raysOut[2].HitFraction);
        _nnInputs.Add(raysOut[3].HitFraction);
        _nnInputs.Add(raysOut[4].HitFraction);
        _nnInputs.Add(raysOut[5].HitFraction);
        _nnInputs.Add(raysOut[6].HitFraction);
        _nnInputs.Add(_nnSpeed);
        _nnInputs.Add(_nnAcceleration);
        _nnInputs.Add(distanceTraveledSinceCheckpoint / 20f);
        _nnInputs.Add(distanceToNextGate / 20f);
        _nnInputs.Add(distanceToPrevGate / 30f);
    }

    private float CalculateReward()
    {
        var currentDistance = Vector3.Distance(transform.position, _lastPosition);
        var distanceValue = (currentSpeed >= 0)
            ? currentDistance
            : -currentDistance;
        distanceTraveledSinceCheckpoint += currentDistance;

        return ((distanceValue * distanceMultiplier) + (currentSpeed * speedMultiplier)) / 10;
    }

    private void MoveCar(float inputVertical, float inputHorizontal)
    {
        _lastPosition = transform.position;
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
        _nnAcceleration = currentAcceleration / (acceleration * 2);

        transform.Translate(Vector3.forward * (currentSpeed * Time.deltaTime));
        if (currentSpeed != 0)
        {
            transform.eulerAngles += new Vector3(0, (inputHorizontal * 90) * turnSpeed, 0);
        }

        _lastSpeed = currentSpeed;
    }
}