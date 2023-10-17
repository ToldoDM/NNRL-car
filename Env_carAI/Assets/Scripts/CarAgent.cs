using System;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class CarAgent : Agent
{
    private Vector3 _startPosition, _startRotation;

    [SerializeField] private List<Transform> spawnPoints;
    [SerializeField] private List<Transform> checkpoints;

    [Header("CurrentStats")] public float currentSpeed;
    public float currentAcceleration;
    public float timeSinceLastCheckpoint = 0f;
    public float distanceToNextGate = -1f;
    public float distanceToPrevGate = -1f;
    public float orientation = 0f;
    public float overallFitness = 0f;
    public bool isTimeoutOn = true;

    [Header("NNet stats")] public float nnSpeed = 0f;
    public float nnAcceleration = 0f;
    public float nnNextGate = 1f;
    public float nnPrevGate = 1f;
    public float nnOrientation = 0f;

    [Header("CarStats")] public float maxSpeed = 11.4f;
    public float acceleration = 5f;
    public float deceleration = 2f;
    public float turnSpeed = 0.02f;

    [Header("Fitness")] public float gateMultiplier = 0f;
    public float maxNnGateDistance = 20f;

    private Vector3 _lastPosition;
    private int _isGoingInt;
    private float _lastSpeed = 0f;
    private int _lastCheckpoint = -1;
    private string _spawnName = "";
    private Vector3 _nextGatePosition = Vector3.zero;
    private Vector3 _prevGatePosition = Vector3.zero;
    private float _prevDistanceToNextGate = 0f;

    public override void OnEpisodeBegin()
    {
        Reset();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(nnSpeed);
        sensor.AddObservation(nnAcceleration);
        sensor.AddObservation(nnNextGate);
        sensor.AddObservation(nnPrevGate);
        sensor.AddObservation(nnOrientation);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        var a = actions.DiscreteActions[0] switch
        {
            > 1 => 1,
            < 1 => -1,
            _ => 0
        };
        var s = actions.DiscreteActions[1] switch
        {
            > 1 => 1,
            < 1 => -1,
            _ => 0
        };
        MoveCar(a, s);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActions = actionsOut.DiscreteActions;
        discreteActions[0] = Input.GetAxis("Vertical") switch
        {
            > 0 => 2,
            < 0 => 0,
            _ => 1
        };
        discreteActions[1] = Input.GetAxis("Horizontal") switch
        {
            > 0 => 2,
            < 0 => 0,
            _ => 1
        };
    }

    public void Reset()
    {
        currentSpeed = 0f;
        currentAcceleration = 0f;
        _lastSpeed = 0f;
        _lastCheckpoint = -1;
        timeSinceLastCheckpoint = 0f;
        _spawnName = "";
        distanceToNextGate = -1f;
        distanceToPrevGate = -1f;
        nnSpeed = 0f;
        nnAcceleration = 0f;
        nnNextGate = 1f;
        nnPrevGate = 1f;
        nnOrientation = 0f;
        orientation = 0f;
        _nextGatePosition = Vector3.zero;
        _prevGatePosition = Vector3.zero;
        overallFitness = 0f;
        _isGoingInt = Convert.ToInt32(currentSpeed >= 0);
        GetRandomPosition();
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
                }

                break;
            default:
                AddReward(-10f);
                overallFitness += -10f;
                EndEpisode();
                Reset();
                break;
        }
    }

    private void FixedUpdate()
    {
        timeSinceLastCheckpoint += Time.deltaTime;
        var position = transform.position;
        distanceToNextGate = Vector3.Distance(position, _nextGatePosition);
        distanceToPrevGate = Vector3.Distance(position, _prevGatePosition);
        nnNextGate = (distanceToNextGate > maxNnGateDistance) ? 1f : (distanceToNextGate / maxNnGateDistance);
        nnPrevGate = (distanceToPrevGate > maxNnGateDistance) ? 1f : (distanceToPrevGate / maxNnGateDistance);
        Debug.DrawLine(position, _nextGatePosition, Color.magenta);
        Debug.DrawLine(position, _prevGatePosition, Color.blue);
        orientation = Vector3.Angle(position - _nextGatePosition, transform.forward);
        nnOrientation = orientation / 180;
        var distance = _prevDistanceToNextGate - distanceToNextGate;
        var currReward = (currentSpeed * distance) * _isGoingInt;
        AddReward(currReward);
        overallFitness += (currReward);
        _prevDistanceToNextGate = distanceToNextGate;

        if (timeSinceLastCheckpoint <= 10f || !isTimeoutOn) return;
        EndEpisode();
        Reset();
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

            AddReward((float)(-Math.Pow(2, exponent) + 1) * gateMultiplier);
            overallFitness += (float)(-Math.Pow(2, exponent) + 1) * gateMultiplier;
        }
        else
        {
            GivePrevNextGate(index);
        }
    }

    private void MoveCar(float inputVertical, float inputHorizontal)
    {
        _lastPosition = transform.position;
        _isGoingInt = Convert.ToInt32(currentSpeed >= 0);
        currentSpeed = inputVertical switch
        {
            > 0 => Mathf.MoveTowards(currentSpeed, maxSpeed,
                acceleration * (2 - _isGoingInt) * Time.deltaTime),
            < 0 => Mathf.MoveTowards(currentSpeed, -maxSpeed / 2,
                acceleration * (_isGoingInt + 1) * Time.deltaTime),
            _ => Mathf.MoveTowards(currentSpeed, 0, deceleration * Time.deltaTime)
        };
        nnSpeed = currentSpeed / maxSpeed;

        currentAcceleration = (currentSpeed - _lastSpeed) / Time.deltaTime;
        nnAcceleration = currentAcceleration / acceleration / 2;

        transform.Translate(Vector3.forward * (currentSpeed * Time.deltaTime));
        if (currentSpeed != 0)
        {
            transform.eulerAngles += new Vector3(0, (inputHorizontal * 90) * turnSpeed, 0);
        }

        _lastSpeed = currentSpeed;
    }

    private void GetRandomPosition()
    {
        var carObject = transform;
        var spawnTransform = spawnPoints[Random.Range(0, spawnPoints.Count)];
        var position = spawnTransform.position;
        var eulerAngles = spawnTransform.eulerAngles + new Vector3(0, Random.Range(0, 36) * 10, 0);
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
        _nextGatePosition = checkpoints[(_lastCheckpoint + 1) % checkpoints.Count].position;
        _prevGatePosition = checkpoints[(_lastCheckpoint + checkpoints.Count - 1) % checkpoints.Count].position;
        _prevDistanceToNextGate = Vector3.Distance(transform.position, _nextGatePosition);
    }
}