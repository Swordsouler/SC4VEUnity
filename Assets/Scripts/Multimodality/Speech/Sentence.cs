using System;
using System.Collections.Generic;

namespace Sven.Command
{
    public class Sentence
    {
        private readonly string _text;
        public string Text => _text;

        private readonly List<Word> _words;
        public List<Word> Words => _words;

        public DateTime StartedAt => _words.Count > 0 ? _words[0].StartedAt : DateTime.MinValue;
        public DateTime EndedAt => _words.Count > 0 ? _words[^1].EndedAt : DateTime.MinValue;

        public Sentence(string text)
        {
            _text = text;
            // split text and make a delay of 1 seconds between each word
            _words = new List<Word>();
            string[] wordArray = text.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            DateTime currentTime = DateTime.Now.AddSeconds(wordArray.Length);
            foreach (string word in wordArray)
            {
                _words.Add(new Word(word, currentTime, currentTime.AddSeconds(1)));
                currentTime = currentTime.AddSeconds(1);
            }
            _words.Sort((x, y) => x.StartedAt.CompareTo(y.StartedAt));
        }
        public Sentence(string text, List<Word> words)
        {
            _text = text;
            _words = words ?? new List<Word>();
            _words.Sort((x, y) => x.StartedAt.CompareTo(y.StartedAt));
        }
    }
}