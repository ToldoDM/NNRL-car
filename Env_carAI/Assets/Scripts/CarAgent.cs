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
    public float gateMultiplier = 0f;
    public int giveReward = 0;

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
        sensor.AddObservation(currentSpeed);
        sensor.AddObservation(currentAcceleration);
        // sensor.AddObservation(timeSinceLastCheckpoint);
        sensor.AddObservation(distanceTraveledSinceCheckpoint);
        sensor.AddObservation(distanceToNextGate);
        sensor.AddObservation(distanceToPrevGate);
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
        currentReward = CalculateReward();
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
        distanceTraveledSinceCheckpoint = 0f;
        giveReward = 0;
        _spawnName = "";
        distanceToNextGate = -1f;
        distanceToPrevGate = -1f;
        _nextGatePosition = Vector3.zero;
        _prevGatePosition = Vector3.zero;
        overallFitness = 0f;
        _isGoingInt = Convert.ToInt32(currentSpeed >= 0);;
        GetRandomPosition();
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
        Debug.DrawLine(position, _nextGatePosition, Color.magenta);
        Debug.DrawLine(position, _prevGatePosition, Color.blue);
        var distance = _prevDistanceToNextGate - distanceToNextGate;
        var currReward = (currentSpeed * distance) * _isGoingInt;
        AddReward(currReward);
        overallFitness += (currReward);
        _prevDistanceToNextGate = distanceToNextGate;

        if (timeSinceLastCheckpoint <= 10f) return;
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
            var reward = distanceTraveledSinceCheckpoint / timeSinceLastCheckpoint;
            AddReward(reward < 1
                ? 1 * gateMultiplier
                : reward * gateMultiplier);
            overallFitness += reward < 1
                ? 1 * gateMultiplier
                : reward * gateMultiplier;
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

        currentAcceleration = (currentSpeed - _lastSpeed) / Time.deltaTime;

        transform.Translate(Vector3.forward * (currentSpeed * Time.deltaTime));
        if (currentSpeed != 0)
        {
            transform.eulerAngles += new Vector3(0, (inputHorizontal * 90) * turnSpeed, 0);
        }

        _lastSpeed = currentSpeed;
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
}