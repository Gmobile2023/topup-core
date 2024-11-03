using System;
using System.Collections.Generic;

namespace Orleans.Sagas
{
    [GenerateSerializer]
    public class SagaState
    {
        [Id(0)]
        public List<ActivityDefinition> Activities { get; set; }

        [Id(1)]
        public int NumCompletedActivities { get; set; }

        [Id(2)]
        public SagaStatus Status { get; set; }

        [Id(3)]
        public int CompensationIndex { get; set; }

        [Id(4)]
        public bool HasBeenAborted { get; set; }

        [Id(5)]
        public Dictionary<string, string> Properties { get; set; }
    }
}