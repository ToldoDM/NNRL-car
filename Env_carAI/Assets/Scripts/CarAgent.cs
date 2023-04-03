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

    [Header("CarStats")] public float maxSpeed = 11.4f;
    public float acceleration = 5f;
    public float deceleration = 2f;
    public float turnSpeed = 0.02f;

    [Header("Fitness")] public float currentReward = 0f;
    public float distanceMultiplier = 1.4f;
    public float speedMultiplier = 0.2f;
    public float gateMultiplier = 1f;

    private Vector3 _lastPosition;
    private float _lastSpeed = 0f;
    private int _lastCheckpoint = -1;

    public override void OnEpisodeBegin()
    {
        GetRandomPosition();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(currentSpeed);
        sensor.AddObservation(currentAcceleration);
        sensor.AddObservation(timeSinceLastCheckpoint);
        sensor.AddObservation(distanceTraveledSinceCheckpoint);
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
        AddReward(currentReward);
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
        GetRandomPosition();
        currentSpeed = 0f;
        currentAcceleration = 0f;
        _lastSpeed = 0f;
        _lastCheckpoint = -1;
        timeSinceLastCheckpoint = 0f;
        distanceTraveledSinceCheckpoint = 0f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.tag.Equals("checkpoints"))
        {
            HandleCheckpoint(other.gameObject.transform);
        }
        else
        {
            AddReward(-10f);
            Reset();
            EndEpisode();
        }
    }

    private void FixedUpdate()
    {
        timeSinceLastCheckpoint += Time.deltaTime;
    }

    private void HandleCheckpoint(Transform checkpoint)
    {
        var index = checkpoints.IndexOf(checkpoint);
        if (_lastCheckpoint < 0)
        {
            _lastCheckpoint = index;
            timeSinceLastCheckpoint = 0f;
            distanceTraveledSinceCheckpoint = 0f;
            AddReward(1f * gateMultiplier);
            return;
        }

        if (index != (_lastCheckpoint + 1) % checkpoints.Count)
        {
            var exponent = 0;
            for (var i = index; i % checkpoints.Count != _lastCheckpoint; i++)
            {
                exponent++;
            }

            AddReward((float)-Math.Pow(2, exponent) + 1);
        }
        else
        {
            var reward = distanceTraveledSinceCheckpoint / timeSinceLastCheckpoint;
            AddReward(reward < 1
                ? 1 * gateMultiplier
                : reward * gateMultiplier);
            _lastCheckpoint = index;
            timeSinceLastCheckpoint = 0f;
            distanceTraveledSinceCheckpoint = 0f;
        }
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
    }
}