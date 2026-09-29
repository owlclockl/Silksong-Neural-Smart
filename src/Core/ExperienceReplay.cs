using System;
using System.Collections.Generic;

namespace SilksongNeuralSmart.Core
{
    public struct Experience
    {
        public float[] State;
        public int Action;
        public float Reward;
        public float[] NextState;
        public bool Done;
        public float Priority;
    }

    public class ExperienceReplayBuffer
    {
        private readonly Experience[] _buffer;
        private int _capacity;
        private int _index;
        private int _count;
        private readonly Random _rng = new Random();

        public int Count => _count;

        public ExperienceReplayBuffer(int capacity = 5000)
        {
            _capacity = capacity;
            _buffer = new Experience[capacity];
            _index = 0;
            _count = 0;
        }

        public void Add(float[] state, int action, float reward, float[] nextState, bool done)
        {
            _buffer[_index] = new Experience
            {
                State = state,
                Action = action,
                Reward = reward,
                NextState = nextState,
                Done = done,
                Priority = Math.Abs(reward) + 0.1f
            };

            _index = (_index + 1) % _capacity;
            if (_count < _capacity) _count++;
        }

        public List<Experience> SampleBatch(int batchSize)
        {
            var batch = new List<Experience>(batchSize);
            if (_count == 0) return batch;

            for (int i = 0; i < batchSize; i++)
            {
                int randIdx = _rng.Next(_count);
                batch.Add(_buffer[randIdx]);
            }

            return batch;
        }

        public void Clear()
        {
            _index = 0;
            _count = 0;
        }
    }
}
