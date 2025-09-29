using System;

namespace Sven.Command
{
    [Serializable]
    public class Word
    {
        private readonly string _text;
        public string Text => _text;

        private readonly DateTime _startedAt;
        public DateTime StartedAt => _startedAt;

        private readonly DateTime _endedAt;
        public DateTime EndedAt => _endedAt;

        public Word(string text, DateTime startedAt, DateTime endedAt)
        {
            _text = text;
            _startedAt = startedAt;
            _endedAt = endedAt;
        }

        public override string ToString()
        {
            return $"{_text} ({_startedAt:HH:mm:ss} - {_endedAt:HH:mm:ss})";
        }
    }
}