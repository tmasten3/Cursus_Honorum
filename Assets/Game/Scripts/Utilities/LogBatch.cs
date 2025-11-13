using System.Collections.Generic;

namespace Game.Core
{
    public class LogBatch
    {
        private readonly string _category;
        private readonly string _summaryMessage;
        private readonly string _fileName;
        private readonly List<string> _records = new();

        public LogBatch(string category, string summaryMessage, string fileName)
        {
            _category = category;
            _summaryMessage = summaryMessage;
            _fileName = fileName;
        }

        public void Add(string message)
        {
            _records.Add(message);
        }

        public void Flush()
        {
            if (_records.Count == 0)
                return;

            Game.Core.Logger.Warn(_category, $"{_records.Count} {_summaryMessage}. See '{_fileName}' for details.");
            Game.Core.Logger.WriteToFile(_fileName, _records);
            _records.Clear();
        }
    }
}
