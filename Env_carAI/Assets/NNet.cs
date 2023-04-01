using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MathNet.Numerics.LinearAlgebra;
using System;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class NNet : MonoBehaviour
{
    private Matrix<float> _inputLayer;
    private Matrix<float> _outputLayer;
    private readonly List<Matrix<float>> _hiddenLayers = new List<Matrix<float>>();
    private readonly List<Matrix<float>> _weights = new List<Matrix<float>>();
    public List<float> biases = new List<float>();

    private int _inputNum, _outputNum;
    public float fitness;

    public void Initialise(int hiddenLayerCount, int hiddenNeuronCount, int inputNum, int outputNum)
    {
        _inputNum = inputNum;
        _outputNum = outputNum;
        _inputLayer = Matrix<float>.Build.Dense(1, _inputNum);
        _outputLayer = Matrix<float>.Build.Dense(1, _outputNum);
        _hiddenLayers.Clear();
        _weights.Clear();
        biases.Clear();

        for (var i = 0; i < hiddenLayerCount + 1; i++)
        {
            var f = Matrix<float>.Build.Dense(1, hiddenNeuronCount);

            _hiddenLayers.Add(f);
            biases.Add(Random.Range(-1f, 1f));

            if (i == 0)
            {
                var inputToH1 = Matrix<float>.Build.Dense(7, hiddenNeuronCount);
                _weights.Add(inputToH1);
            }

            var hiddenToHidden = Matrix<float>.Build.Dense(hiddenNeuronCount, hiddenNeuronCount);
            _weights.Add(hiddenToHidden);
        }

        var outputWeight = Matrix<float>.Build.Dense(hiddenNeuronCount, 2);
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
}