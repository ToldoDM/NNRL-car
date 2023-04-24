using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;
using System.IO;

public class GeneticManager : MonoBehaviour
{
    [Header("Controls")] [Range(0.0f, 0.1f)]
    public float mutationRate = 0.055f;

    [Range(0.0f, 0.05f)] public float randomChild = 0.055f;

    [Header("Crossover Controls")] [Range(0, 100)]
    public int bestAgentSelectionPercent = 20;

    [Header("Public View")] public int currentGeneration = 1;
    public int initialPopulation;
    public int currentPopulation;

    [Header("Saving Stats")] public int nNetSavingInterval = 100;

    private readonly List<CarController> _population = new List<CarController>();
    private int _topPercentNumber = 0;

    private readonly Dictionary<int, List<float>> _currentGenStats = new Dictionary<int, List<float>>();
    
    
    private void Start()
    {
        // Check if the directory already exists
        if (!Directory.Exists("GA_training"))
        {
            // If the directory doesn't exist, create it
            Directory.CreateDirectory("GA_training");
        }
    }

    public void AddCar(CarController car)
    {
        initialPopulation++;
        currentPopulation++;
        _population.Add(car);
    }

    public void Death()
    {
        currentPopulation--;
        if (currentPopulation == 0)
        {
            _population.Sort((x, y) => x.overallFitness.CompareTo(y.overallFitness));
            _population.Reverse();
            SaveStats();
            RePopulate();
        }
    }

    private void SaveStats()
    {
        _currentGenStats.Add(currentGeneration, _population.Select(p => p.overallFitness).ToList());

        // Create a new StreamWriter object and open the file
        using (StreamWriter writer = new StreamWriter("GA_training/stats.csv"))
        {
            // Loop through each row of the matrix
            foreach (var generation in _currentGenStats)
            {
                writer.Write(generation.Key + ",");
                // Loop through each column of the matrix
                for (var i = 0; i < generation.Value.Count; i++)
                {
                    // Write the value to the CSV file, followed by a comma
                    writer.Write(generation.Value[i] + ",");
                }

                // Write a new line character to separate rows
                writer.WriteLine();
            }
        }

        if (currentGeneration % nNetSavingInterval != 0) return;
        var topPercentList = GetTopPercent();
        for (var i = 0; i < topPercentList.Count; i++)
        {
            topPercentList[i].network.SaveToCsv("GA_training/", i, currentGeneration);
        }
    }

    private List<CarController> GetTopPercent()
    {
        _topPercentNumber = _population.Count * bestAgentSelectionPercent / 100;
        if (_topPercentNumber == 0) _topPercentNumber = 1;
        return _population.Take(_topPercentNumber).ToList();
    }

    private void RePopulate()
    {
        var topPercentList = GetTopPercent();
        currentPopulation = topPercentList.Count;

        for (int i = _topPercentNumber; i < _population.Count; i++)
        {
            //from the top % I'll select two random parents and i'll fill the remaining population with their children
            var parent1 = Random.Range(0, _topPercentNumber);
            var parent2 = Random.Range(0, _topPercentNumber);

            //There are some % for totally random and single node random weights
            _population[i].network.Crossover(topPercentList[parent1].network, topPercentList[parent2].network,
                mutationRate);
            if (Random.value > randomChild)
            {
                _population[i].network.RandomiseWeightAndBias();
            }

            currentPopulation++;
        }

        NextGeneration();
    }

    private void NextGeneration()
    {
        currentGeneration++;
        // always same spawn point for everyone
        var index = 1;//Random.Range(0, _population[0].spawnPoints.Count);
        foreach (var car in _population)
        {
            car.Reset(index);
        }
    }
}