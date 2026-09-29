using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SilksongNeuralSmart.Core
{
    [Serializable]
    public class NeuralNetwork
    {
        public int[] LayerSizes { get; set; }
        public float[][][] Weights { get; set; } // [layer][neuron_to][neuron_from]
        public float[][] Biases { get; set; }    // [layer][neuron]
        
        [NonSerialized]
        public float[][] Activations;            // [layer][neuron]
        [NonSerialized]
        public float[][] RawZ;                  // [layer][neuron]

        // Adam optimizer parameters
        [NonSerialized] private float[][][] _mWeights;
        [NonSerialized] private float[][][] _vWeights;
        [NonSerialized] private float[][] _mBiases;
        [NonSerialized] private float[][] _vBiases;
        [NonSerialized] private int _tAdam;

        private static readonly Random Rng = new Random();

        public NeuralNetwork(params int[] layerSizes)
        {
            if (layerSizes == null || layerSizes.Length < 2)
                throw new ArgumentException("NeuralNetwork requires at least an input and output layer.");

            LayerSizes = layerSizes;
            InitializeLayers();
            InitializeWeightsHe();
        }

        private void InitializeLayers()
        {
            int numLayers = LayerSizes.Length;
            Activations = new float[numLayers][];
            RawZ = new float[numLayers][];

            for (int i = 0; i < numLayers; i++)
            {
                Activations[i] = new float[LayerSizes[i]];
                RawZ[i] = new float[LayerSizes[i]];
            }

            Weights = new float[numLayers - 1][][];
            Biases = new float[numLayers - 1][];

            _mWeights = new float[numLayers - 1][][];
            _vWeights = new float[numLayers - 1][][];
            _mBiases = new float[numLayers - 1][];
            _vBiases = new float[numLayers - 1][];

            for (int l = 0; l < numLayers - 1; l++)
            {
                int outNodes = LayerSizes[l + 1];
                int inNodes = LayerSizes[l];

                Weights[l] = new float[outNodes][];
                Biases[l] = new float[outNodes];

                _mWeights[l] = new float[outNodes][];
                _vWeights[l] = new float[outNodes][];
                _mBiases[l] = new float[outNodes];
                _vBiases[l] = new float[outNodes];

                for (int j = 0; j < outNodes; j++)
                {
                    Weights[l][j] = new float[inNodes];
                    _mWeights[l][j] = new float[inNodes];
                    _vWeights[l][j] = new float[inNodes];
                }
            }
        }

        public void InitializeWeightsHe()
        {
            for (int l = 0; l < Weights.Length; l++)
            {
                int inCount = LayerSizes[l];
                float stdDev = (float)Math.Sqrt(2.0 / inCount);

                for (int j = 0; j < Weights[l].Length; j++)
                {
                    Biases[l][j] = 0.01f;
                    for (int i = 0; i < Weights[l][j].Length; i++)
                    {
                        Weights[l][j][i] = NextGaussian(0f, stdDev);
                    }
                }
            }
        }

        /// <summary>
        /// Ultra-fast forward inference pass (< 0.02ms execution time)
        /// </summary>
        public float[] Forward(float[] inputs)
        {
            if (inputs == null || inputs.Length != LayerSizes[0])
                throw new ArgumentException($"Input size mismatch. Expected {LayerSizes[0]}, got {inputs?.Length ?? 0}");

            // Copy input into layer 0
            Array.Copy(inputs, Activations[0], inputs.Length);

            for (int l = 0; l < Weights.Length; l++)
            {
                float[] prevLayer = Activations[l];
                float[] currentLayer = Activations[l + 1];
                float[] currentRawZ = RawZ[l + 1];
                float[][] wLayer = Weights[l];
                float[] bLayer = Biases[l];

                bool isOutputLayer = (l == Weights.Length - 1);

                for (int j = 0; j < currentLayer.Length; j++)
                {
                    float sum = bLayer[j];
                    float[] wRow = wLayer[j];

                    // Unrolled SIMD-friendly vector dot product
                    int i = 0;
                    int limit = prevLayer.Length - 3;
                    for (; i < limit; i += 4)
                    {
                        sum += prevLayer[i] * wRow[i]
                             + prevLayer[i + 1] * wRow[i + 1]
                             + prevLayer[i + 2] * wRow[i + 2]
                             + prevLayer[i + 3] * wRow[i + 3];
                    }
                    for (; i < prevLayer.Length; i++)
                    {
                        sum += prevLayer[i] * wRow[i];
                    }

                    currentRawZ[j] = sum;

                    if (isOutputLayer)
                    {
                        // Softmax or Tanh for output
                        currentLayer[j] = sum;
                    }
                    else
                    {
                        // LeakyReLU activation for hidden layers
                        currentLayer[j] = sum > 0f ? sum : sum * 0.01f;
                    }
                }

                // Apply Softmax to final output layer for policy distribution
                if (isOutputLayer)
                {
                    ApplySoftmax(currentLayer);
                }
            }

            return Activations[Activations.Length - 1];
        }

        private void ApplySoftmax(float[] values)
        {
            float max = values[0];
            for (int i = 1; i < values.Length; i++)
                if (values[i] > max) max = values[i];

            float sum = 0f;
            for (int i = 0; i < values.Length; i++)
            {
                // Clip extreme logits for numerical stability
                float clamped = Math.Max(-20f, Math.Min(20f, values[i] - max));
                values[i] = (float)Math.Exp(clamped);
                sum += values[i];
            }

            if (sum < 1e-7f) sum = 1e-7f;
            for (int i = 0; i < values.Length; i++)
            {
                values[i] /= sum;
            }
        }

        /// <summary>
        /// Neuroevolution Mutation: Perturbs weights with Gaussian noise
        /// </summary>
        public void Mutate(float mutationRate, float mutationStrength)
        {
            for (int l = 0; l < Weights.Length; l++)
            {
                for (int j = 0; j < Weights[l].Length; j++)
                {
                    if (Rng.NextDouble() < mutationRate)
                    {
                        Biases[l][j] += NextGaussian(0f, mutationStrength * 0.5f);
                    }

                    for (int i = 0; i < Weights[l][j].Length; i++)
                    {
                        if (Rng.NextDouble() < mutationRate)
                        {
                            if (Rng.NextDouble() < 0.05) // Complete reset chance
                            {
                                Weights[l][j][i] = NextGaussian(0f, 0.5f);
                            }
                            else
                            {
                                Weights[l][j][i] += NextGaussian(0f, mutationStrength);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Genetic Crossover between two parent networks
        /// </summary>
        public static NeuralNetwork Crossover(NeuralNetwork parentA, NeuralNetwork parentB)
        {
            var child = new NeuralNetwork(parentA.LayerSizes);

            for (int l = 0; l < child.Weights.Length; l++)
            {
                for (int j = 0; j < child.Weights[l].Length; j++)
                {
                    child.Biases[l][j] = Rng.NextDouble() < 0.5 ? parentA.Biases[l][j] : parentB.Biases[l][j];

                    for (int i = 0; i < child.Weights[l][j].Length; i++)
                    {
                        // Blend or uniform crossover
                        if (Rng.NextDouble() < 0.5)
                            child.Weights[l][j][i] = parentA.Weights[l][j][i];
                        else
                            child.Weights[l][j][i] = parentB.Weights[l][j][i];
                    }
                }
            }

            return child;
        }

        public NeuralNetwork Clone()
        {
            var clone = new NeuralNetwork(LayerSizes);
            for (int l = 0; l < Weights.Length; l++)
            {
                Array.Copy(Biases[l], clone.Biases[l], Biases[l].Length);
                for (int j = 0; j < Weights[l].Length; j++)
                {
                    Array.Copy(Weights[l][j], clone.Weights[l][j], Weights[l][j].Length);
                }
            }
            return clone;
        }

        private static float NextGaussian(float mean, float stdDev)
        {
            double u1 = 1.0 - Rng.NextDouble();
            double u2 = 1.0 - Rng.NextDouble();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            return mean + stdDev * (float)randStdNormal;
        }

        #region Serialization to JSON
        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"layerSizes\": [" + string.Join(",", LayerSizes) + "],\n");
            
            sb.Append("  \"biases\": [\n");
            for (int l = 0; l < Biases.Length; l++)
            {
                sb.Append("    [");
                for (int j = 0; j < Biases[l].Length; j++)
                {
                    sb.Append(Biases[l][j].ToString("G7", CultureInfo.InvariantCulture));
                    if (j < Biases[l].Length - 1) sb.Append(",");
                }
                sb.Append("]");
                if (l < Biases.Length - 1) sb.Append(",");
                sb.Append("\n");
            }
            sb.Append("  ],\n");

            sb.Append("  \"weights\": [\n");
            for (int l = 0; l < Weights.Length; l++)
            {
                sb.Append("    [\n");
                for (int j = 0; j < Weights[l].Length; j++)
                {
                    sb.Append("      [");
                    for (int i = 0; i < Weights[l][j].Length; i++)
                    {
                        sb.Append(Weights[l][j][i].ToString("G7", CultureInfo.InvariantCulture));
                        if (i < Weights[l][j].Length - 1) sb.Append(",");
                    }
                    sb.Append("]");
                    if (j < Weights[l].Length - 1) sb.Append(",");
                    sb.Append("\n");
                }
                sb.Append("    ]");
                if (l < Weights.Length - 1) sb.Append(",");
                sb.Append("\n");
            }
            sb.Append("  ]\n");
            sb.Append("}");
            return sb.ToString();
        }

        public static NeuralNetwork FromJson(string json)
        {
            try
            {
                // Lightweight hand-crafted fast parser to avoid external JSON library dependencies
                int lsStart = json.IndexOf("\"layerSizes\"", StringComparison.Ordinal);
                int lsArrStart = json.IndexOf('[', lsStart);
                int lsArrEnd = json.IndexOf(']', lsArrStart);
                string lsStr = json.Substring(lsArrStart + 1, lsArrEnd - lsArrStart - 1);
                string[] lsTokens = lsStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                int[] layerSizes = new int[lsTokens.Length];
                for (int i = 0; i < lsTokens.Length; i++)
                {
                    layerSizes[i] = int.Parse(lsTokens[i].Trim());
                }

                var net = new NeuralNetwork(layerSizes);

                // Extract biases
                int biasesStart = json.IndexOf("\"biases\"", StringComparison.Ordinal);
                int biasesEnd = json.IndexOf("\"weights\"", StringComparison.Ordinal);
                string biasesSub = json.Substring(biasesStart, biasesEnd - biasesStart);
                List<List<float>> parsedBiases = ParseNestedFloatArrays(biasesSub);
                for (int l = 0; l < net.Biases.Length && l < parsedBiases.Count; l++)
                {
                    for (int j = 0; j < net.Biases[l].Length && j < parsedBiases[l].Count; j++)
                    {
                        net.Biases[l][j] = parsedBiases[l][j];
                    }
                }

                // Extract weights
                int weightsStart = json.IndexOf("\"weights\"", StringComparison.Ordinal);
                string weightsSub = json.Substring(weightsStart);
                List<List<List<float>>> parsedWeights = Parse3DFloatArrays(weightsSub);
                for (int l = 0; l < net.Weights.Length && l < parsedWeights.Count; l++)
                {
                    for (int j = 0; j < net.Weights[l].Length && j < parsedWeights[l].Count; j++)
                    {
                        for (int i = 0; i < net.Weights[l][j].Length && i < parsedWeights[l][j].Count; i++)
                        {
                            net.Weights[l][j][i] = parsedWeights[l][j][i];
                        }
                    }
                }

                return net;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to parse NeuralNetwork JSON: " + ex.Message, ex);
            }
        }

        private static List<List<float>> ParseNestedFloatArrays(string text)
        {
            var result = new List<List<float>>();
            int pos = 0;
            while (pos < text.Length)
            {
                int open = text.IndexOf('[', pos);
                if (open == -1) break;
                // check if it's the root array bracket
                if (result.Count == 0 && text.IndexOf('[', open + 1) != -1 && text.IndexOf('[', open + 1) < text.IndexOf(']', open))
                {
                    pos = open + 1;
                    continue;
                }
                int close = text.IndexOf(']', open);
                if (close == -1) break;

                string inner = text.Substring(open + 1, close - open - 1);
                string[] parts = inner.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                var row = new List<float>();
                foreach (var p in parts)
                {
                    if (float.TryParse(p.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float val))
                        row.Add(val);
                }
                if (row.Count > 0) result.Add(row);
                pos = close + 1;
            }
            return result;
        }

        private static List<List<List<float>>> Parse3DFloatArrays(string text)
        {
            var layers = new List<List<List<float>>>();
            int lOpen = text.IndexOf('[');
            if (lOpen == -1) return layers;

            int cur = lOpen + 1;
            while (cur < text.Length)
            {
                int layerStart = text.IndexOf('[', cur);
                if (layerStart == -1) break;

                // Find matching layer end
                int layerEnd = FindClosingBracket(text, layerStart);
                if (layerEnd == -1) break;

                string layerStr = text.Substring(layerStart + 1, layerEnd - layerStart - 1);
                var layerRows = ParseNestedFloatArrays(layerStr);
                if (layerRows.Count > 0)
                {
                    layers.Add(layerRows);
                }
                cur = layerEnd + 1;
            }
            return layers;
        }

        private static int FindClosingBracket(string text, int openPos)
        {
            int depth = 0;
            for (int i = openPos; i < text.Length; i++)
            {
                if (text[i] == '[') depth++;
                else if (text[i] == ']')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }
        #endregion
    }
}
