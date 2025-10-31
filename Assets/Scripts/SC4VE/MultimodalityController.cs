using NaughtyAttributes;
using Newtonsoft.Json;
using Sc4ve.Voice;
using Sven.OwlTime;
using System.Collections.Generic;
using UnityEngine;

namespace Sc4ve.Multimodality
{
    public class MultimodalityController : MonoBehaviour
    {
        [BoxGroup("References"), SerializeField] private VoskSpeechToText _voskSpeechToText;

        private void Awake()
        {
            if (_voskSpeechToText != null) _voskSpeechToText.OnTranscriptionResult += OnTranscriptionResult;
        }

        private void OnTranscriptionResult(string obj)
        {
            var result = new RecognitionResult(obj);
            for (int i = 0; i < result.Phrases.Length; i++)
            {
                if (result.Phrases[i].Text == "") continue;

                Sentence phrase = result.Phrases[i];

                phrase.Start(new Instant(phrase.StartedAt));
                phrase.End(new Instant(phrase.EndedAt));
                phrase.Semanticize();
                /*_commandChain = new CommandChain(_commandExecutionMode, result.Phrases[i], Settings);
                _commandChain.Execute();*/
            }
        }

        private List<Command> DeserializeCommand(string json)
        {
            // using newtonsoft json
            List<Command> commands = JsonConvert.DeserializeObject<List<Command>>(json);
            return commands;
        }

        private List<Command> CommandTest1()
        {
            string jsonTest = "[\r\n  {\r\n    \"type\": \"ColorizeCommand\",\r\n    \"parameters\": [\r\n      {\r\n        \"type\": \"ColorParameter\",\r\n        \"value\": \"rouge\"\r\n      }\r\n    ]\r\n  }\r\n]";
            return DeserializeCommand(jsonTest);
        }

        private List<Command> CommandTest2()
        {
            string jsonTest = "[\r\n  {\r\n    \"type\": \"ColorizeCommand\",\r\n    \"parameters\": [\r\n      {\r\n        \"type\": \"ColorParameter\",\r\n        \"value\": \"rouge\"\r\n      },\r\n      {\r\n        \"type\": \"SelectionParameter\",\r\n        \"filters\": [\r\n          {\r\n            \"operator\": \"=\",\r\n            \"type\": \"Annotation\",\r\n            \"value\": \"citrouille\",\r\n            \"timestamp\": \"2025-10-06T10:50:45.1472611+02:00\"\r\n          },\r\n          \"AND\",\r\n          {\r\n            \"operator\": \"=\",\r\n            \"type\": \"PointOfView\",\r\n            \"timestamp\": \"2025-10-06T10:50:46.4247752+02:00\"\r\n          }\r\n        ],\r\n        \"limit\": \"5\",\r\n        \"order\": {\r\n          \"criterias\": [\r\n            {\r\n              \"type\": \"size\",\r\n              \"desc\": true\r\n            },\r\n            {\r\n              \"type\": \"name\",\r\n              \"desc\": false\r\n            }\r\n          ]\r\n        }\r\n      }\r\n    ]\r\n  }\r\n]";
            return DeserializeCommand(jsonTest);
        }

        public void PrintTest()
        {
            Debug.Log(JsonConvert.SerializeObject(CommandTest1()));
            Debug.Log(JsonConvert.SerializeObject(CommandTest2()));
        }
    }
}