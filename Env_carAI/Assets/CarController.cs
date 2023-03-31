using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarController : MonoBehaviour
{
    private Vector3 startPosition, startRotation;
    private readonly int _distanceDivision = 30;

    [Header("CurrentStats")] public float currentSpeed;
    public float currentAcceleration;
    public float timeSinceStart = 0f;
    public float overallFitness;

    [Header("CarStats")] public float maxSpeed = 11.4f;
    public float acceleration = 5f;
    public float deceleration = 2f;
    public float turnSpeed = 0.02f;

    [Header("Fitness")] public float distanceMultipler = 1.4f;
    public float avgSpeedMultiplier = 0.2f;
    public float sensorMultiplier = 0.1f;

    private Vector3 lastPosition;
    private float totalDistanceTravelled;
    private float avgSpeed;

    private float aSensor, bSensor, cSensor;

    private void Awake()
    {
        startPosition = transform.position;
        startRotation = transform.eulerAngles;
    }

    public void Reset()
    {
        currentSpeed = 0f;
        currentAcceleration = 0f;
        timeSinceStart = 0f;
        totalDistanceTravelled = 0f;
        avgSpeed = 0f;
        lastPosition = startPosition;
        overallFitness = 0f;
        transform.position = startPosition;
        transform.eulerAngles = startRotation;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Reset();
        if (!collision.gameObject.name.Equals("Ground"))
        {
            Reset();
        }
    }

    private void FixedUpdate()
    {
        InputSensors();
        lastPosition = transform.position;

        //Neural network code here

        MoveCar();

        timeSinceStart += Time.deltaTime;

        CalculateFitness();

        //a = 0;
        //t = 0;
    }


    private void CalculateFitness()
    {
        totalDistanceTravelled += Vector3.Distance(transform.position, lastPosition);
        avgSpeed = totalDistanceTravelled / timeSinceStart;

        overallFitness = (totalDistanceTravelled * distanceMultipler) + (avgSpeed * avgSpeedMultiplier) +
                         (((aSensor + bSensor + cSensor) / 3) * sensorMultiplier);

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
        Vector3 rayRight = (forward + right);
        Vector3 rayTop = (forward);
        Vector3 rayLeft = (forward - right);

        Ray r = new Ray(transform1.position, rayRight);
        RaycastHit hit;

        if (Physics.Raycast(r, out hit))
        {
            aSensor = hit.distance / _distanceDivision;
            Debug.DrawLine(r.origin, hit.point, Color.red);
            // print("rayRight: " + aSensor);
        }

        r.direction = rayTop;

        if (Physics.Raycast(r, out hit))
        {
            bSensor = hit.distance / _distanceDivision;
            Debug.DrawLine(r.origin, hit.point, Color.red);
            // print("rayTop: " + bSensor);
        }

        r.direction = rayLeft;

        if (Physics.Raycast(r, out hit))
        {
            cSensor = hit.distance / _distanceDivision;
            Debug.DrawLine(r.origin, hit.point, Color.red);
            // print("rayLeft: " + cSensor);
        }
    }

    private void MoveCar()
    {
        float inputVertical = Input.GetAxis("Vertical");
        float inputHorizontal = Input.GetAxis("Horizontal");

        if (inputVertical > 0)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpeed, acceleration * Time.deltaTime);
        }
        else if (inputVertical < 0)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, -maxSpeed/2, acceleration * Time.deltaTime);
        }
        else
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0, deceleration * Time.deltaTime);
        }

        transform.Translate(Vector3.forward * (currentSpeed * Time.deltaTime));

        if (currentSpeed != 0)
        {
            transform.eulerAngles += new Vector3(0, (inputHorizontal * 90) * turnSpeed, 0);
        }
    }
}