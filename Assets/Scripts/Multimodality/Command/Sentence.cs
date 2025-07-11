using System;
using System.Collections.Generic;

namespace Sven.Command
{
    public class Sentence
    {
        private readonly string _text;
        public string Text => _text;

        private readonly DateTime _startedAt;
        public DateTime StartedAt => _startedAt;

        private readonly DateTime _endedAt;
        public DateTime EndedAt => _endedAt;

        private readonly List<Word> _words;
        public List<Word> Words => _words;
    }
}