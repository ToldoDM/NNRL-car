﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MathNet.Numerics.LinearAlgebra;
using System;
using System.IO;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class NNet : MonoBehaviour
{
    [Header("Network Options")] public int layers = 2;
    public int neurons = 128;
    public List<float> biases = new List<float>();

    private Matrix<float> _inputLayer;
    private Matrix<float> _outputLayer;
    private readonly List<Matrix<float>> _hiddenLayers = new List<Matrix<float>>();
    private readonly List<Matrix<float>> _weights = new List<Matrix<float>>();
    private int _inputNum, _outputNum;

    public void Initialise(int inputNum, int outputNum)
    {
        _inputNum = inputNum;
        _outputNum = outputNum;
        _inputLayer = Matrix<float>.Build.Dense(1, _inputNum);
        _outputLayer = Matrix<float>.Build.Dense(1, _outputNum);
        _hiddenLayers.Clear();
        _weights.Clear();
        biases.Clear();

        for (var i = 0; i < layers; i++)
        {
            var f = Matrix<float>.Build.Dense(1, neurons);

            _hiddenLayers.Add(f);
            biases.Add(Random.Range(-1f, 1f));

            if (i == 0)
            {
                var inputToH1 = Matrix<float>.Build.Dense(_inputNum, neurons);
                _weights.Add(inputToH1);
            }

            var hiddenToHidden = Matrix<float>.Build.Dense(neurons, neurons);
            _weights.Add(hiddenToHidden);
        }

        var outputWeight = Matrix<float>.Build.Dense(neurons, _outputNum);
        _weights.Add(outputWeight);
        biases.Add(Random.Range(-1f, 1f));

        RandomiseWeights();
    }

    private void RandomiseWeights()
    {
        foreach (var t in _weights)
        {
            for (var x = 0; x < t.RowCount; x++)
            {
                for (var y = 0; y < t.ColumnCount; y++)
                {
                    t[x, y] = Random.Range(-1f, 1f);
                }
            }
        }
    }

    public List<float> RunNetwork(List<float> inputs)
    {
        for (var i = 0; i < _inputNum; i++)
        {
            _inputLayer[0, i] = inputs[i];
        }

        _inputLayer = _inputLayer.PointwiseTanh();
        _hiddenLayers[0] = ((_inputLayer * _weights[0]) + biases[0]).PointwiseTanh();

        for (var i = 1; i < _hiddenLayers.Count; i++)
        {
            _hiddenLayers[i] = ((_hiddenLayers[i - 1] * _weights[i]) + biases[i]).PointwiseTanh();
        }

        _outputLayer = ((_hiddenLayers[^1] * _weights[^1]) + biases[^1]).PointwiseTanh();

        var output = new List<float>();
        for (var i = 0; i < _outputNum; i++)
        {
            output.Add((float)Math.Tanh(_outputLayer[0, i]));
        }

        return output;
    }

    public void Crossover(NNet p1, NNet p2)
    {
        // var absFitP1P2 = Mathf.Abs(p1Fit) + Mathf.Abs(p2Fit);
        // var p1Weight = absFitP1P2 + p1Fit;
        // var p2Weight = absFitP1P2 + p2Fit;
        for (int i = 0; i < _weights.Count; i++)
        {
            for (int x = 0; x < _weights[i].RowCount; x++)
            {
                for (int y = 0; y < _weights[i].ColumnCount; y++)
                {
                    if (Random.value > 0.5f)
                    {
                        _weights[i][x, y] = p1._weights[i][x, y];
                    }
                    else
                    {
                        _weights[i][x, y] = p2._weights[i][x, y];
                    }
                    // _weights[i][x, y] = (p1._weights[i][x, y] + p2._weights[i][x, y]) / 2;
                    // _weights[i][x, y] = ((p1._weights[i][x, y] * p1Weight) + (p2._weights[i][x, y]) * p2Weight) /
                    //                     (p1Weight + p2Weight);
                }
            }
        }

        // for (int i = 0; i < biases.Count; i++)
        // {
        //     // mix the bias from parent1 and parent2 with a probability of totally new bias
        //     if (Random.value > 0.5f)
        //         biases[i] = p1.biases[i];
        //     else
        //         biases[i] = p2.biases[i];
        //     // biases[i] = (p1.biases[i] + p2.biases[i]) / 2;
        //     // biases[i] = ((p1.biases[i] * p1Weight) + (p2.biases[i]) * p2Weight) / (p1Weight + p2Weight);
        // }
    }
    
    public void Mutate(float mutationRate)
    {
        for (int i = 0; i < _weights.Count; i++)
        {
            for (int x = 0; x < _weights[i].RowCount; x++)
            {
                for (int y = 0; y < _weights[i].ColumnCount; y++)
                {
                    if (Random.value <= mutationRate)
                    {
                        _weights[i][x, y] = Random.Range(-1f, 1f);
                    }
                }
            }
        }

        for (int i = 0; i < biases.Count; i++)
        {
            if (Random.value <= mutationRate)
            {
                biases[i] = Random.Range(-1f, 1f);
            }
        }
    }

    public void RandomiseWeightAndBias()
    {
        for (int i = 0; i < biases.Count; i++)
        {
            biases[i] = Random.Range(-1f, 1f);
        }

        RandomiseWeights();
    }

    public void SaveToCsv(string filePath, int car, int gen)
    {
        // Create a new StreamWriter object and open the file
        using (StreamWriter writer = new StreamWriter(filePath + "NNet_Gen" + gen + "_Car" + car + ".csv"))
        {
            writer.Write(neurons + "," + layers);
            writer.WriteLine();
            writer.Write(_inputNum + "," + _outputNum);
            writer.WriteLine();

            // Loop through each row of the matrix
            for (var i = 0; i < _weights.Count; i++)
            {
                // Loop through each column of the matrix
                for (var x = 0; x < _weights[i].RowCount; x++)
                {
                    for (var y = 0; y < _weights[i].ColumnCount; y++)
                    {
                        // Write the value to the CSV file, followed by a comma
                        writer.Write(_weights[i][x, y] + ",");
                    }
                }

                // Write a new line character to separate rows
                writer.WriteLine();
            }

            for (var i = 0; i < biases.Count; i++)
            {
                writer.Write(biases[i] + ",");
            }

            // Write a new line character to separate rows
            writer.WriteLine();
        }
    }

    public void LoadNet(string csvNnet)
    {
        var lines = csvNnet.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
        var neuronLayers = lines[0].Split(',');
        layers = int.Parse(neuronLayers[1]);
        neurons = int.Parse(neuronLayers[0]);
        var inputOutput = lines[1].Split(',');
        Initialise(int.Parse(inputOutput[0]), int.Parse(inputOutput[1]));

        for (var i = 2; i < lines.Length - 2; i++)
        {
            var values = lines[i].Split(',');
            var valIndex = 0;
            for (var x = 0; x < _weights[i - 2].RowCount; x++)
            {
                for (var y = 0; y < _weights[i - 2].ColumnCount; y++)
                {
                    _weights[i - 2][x, y] = float.Parse(values[valIndex]);
                    valIndex++;
                }
            }
        }

        var fileBiases = lines[^2].Split(',');
        for (var i = 0; i < fileBiases.Length - 1; i++)
        {
            biases[i] = float.Parse(fileBiases[i]);
        }
    }
}