using System;
using System.Collections.Generic;
using System.IO;
using Compression.Utilities;
using Genome;
using OptimizedCore;

namespace SAUtils.InputFileParsers.CustomInterval
{
	public sealed class CustomIntervalParser
	{
		private readonly FileInfo _customFileInfo;
		private readonly List<string> _stringFields;
		private readonly Dictionary<string, string> _stringValues;
		private readonly Dictionary<string, string> _nonstringValues;
		private readonly List<string> _nonstringFields;
		public string KeyName;
		private readonly Dictionary<int, string> _fieldIndex;
        private readonly IDictionary<string, IChromosome> _refChromDict;

        public CustomIntervalParser(FileInfo customFileInfo, IDictionary<string, IChromosome> refChromDict)
		{
            _customFileInfo  = customFileInfo;
            _stringFields    = new List<string>();
            _stringValues    = new Dictionary<string, string>();
            _nonstringFields = new List<string>();
            _nonstringValues = new Dictionary<string, string>();
            _fieldIndex      = new Dictionary<int, string>();
            _refChromDict    = refChromDict;
            ReadHeader();
        }

        private void Clear()
		{
			_stringValues.Clear();
			_nonstringValues.Clear();
		}

		private void ReadHeader()
		{
			using (var reader = GZipUtilities.GetAppropriateStreamReader(_customFileInfo.FullName))
			{
				string line;

				while ((line = reader.ReadLine()) != null)
				{
					// Skip empty lines.
					if (line.IsWhiteSpace()) continue;
					if (line.OptimizedStartsWith('#'))
					{
						ParseHeaderLine(line);
					}
					else
					{
						break;
					}

				}
			}
		}

		public IEnumerable<DataStructures.CustomInterval> GetCustomIntervals()
		{
			using (var reader = GZipUtilities.GetAppropriateStreamReader(_customFileInfo.FullName))
			{
				string line;

				while ((line = reader.ReadLine()) != null)
				{
					// Skip empty lines.
					if (line.IsWhiteSpace()) continue;
					if (line.OptimizedStartsWith('#')) continue;

					var customInterval = ExtractCustomInterval(line);
					if (customInterval == null) continue;
					yield return customInterval;

				}
			}
		}

		private DataStructures.CustomInterval ExtractCustomInterval(string bedLine)
		{
			if (bedLine == null) return null;
			var bedFields = bedLine.OptimizedSplit('\t');

			if (bedFields.Length < BedCommon.MinNoOfFields)
                throw new InvalidDataException($"Bed file line must contain at least {BedCommon.MinNoOfFields} fields. Current line:\n {bedLine}");

            var chromosome = bedFields[BedCommon.ChromIndex];
			if (!_refChromDict.ContainsKey(chromosome)) return null;
		    var chr = _refChromDict[chromosome];

			var start      = Convert.ToInt32(bedFields[BedCommon.StartIndex]) + 1;
			var end        = Convert.ToInt32(bedFields[BedCommon.EndIndex]);

			Clear();
			ParseInfoField(bedFields[BedCommon.InfoIndex]);

			if (_stringValues.Count == 0 && _nonstringFields.Count == 0)
				return null;

			var stringValues = new Dictionary<string, string>();
			var nonStringValues = new Dictionary<string, string>();

			foreach (var keyValue in _stringValues)
			{
				stringValues[keyValue.Key] = keyValue.Value;
			}

			foreach (var keyValue in _nonstringValues)
			{
				nonStringValues[keyValue.Key] = keyValue.Value;
			}

			return new DataStructures.CustomInterval(chr, start, end, KeyName, stringValues, nonStringValues);
		}

	    private void ParseInfoField(string infoFieldsLine)
		{
			// OR4F5;0.0;3.60208899915;Some_evidence_of_constraint
			var infoFields = infoFieldsLine.OptimizedSplit(';');
			for (int i = 0; i < infoFields.Length; i++)
			{
				var key = _fieldIndex[i];
				var value = infoFields[i];

				if (_stringFields.Contains(key))
					_stringValues[key] = value;

				if (_nonstringFields.Contains(key))
					_nonstringValues[key] = value;

			}

		}
		private void ParseHeaderLine(string line)
		{
			//##IAE_TOP=<TYPE=IntervalsXX>
			if (line.StartsWith("##IAE_TOP=")) GetTopLevelKey(line);

			//##IAE_INFO=<INFO=GENE,Type=String,JSON=gene>
			if (line.StartsWith("##IAE_INFO=")) AddInfoField(line);
		}

		private void GetTopLevelKey(string line)
		{
			//##IAE_TOP=<TYPE=IntervalsXX>
			line = line.Substring(11);//removing ##IAE_TOP=<
			line = line.Substring(0, line.Length - 1);//removing the last '>'

		    (string key, string value) = line.OptimizedKeyValue();

            switch (key)
			{
				case "TYPE":
					KeyName = value;
					break;
				default:
					throw new InvalidDataException("Unknown field in top level key line :\n " + line);
			}			
		}

        private void AddInfoField(string line)
        {
            //##IAE_INFO=<INFO=GENE,Type=String,JSON=gene>
            line = line.Substring(12);//removing ##IAE_INFO=<
            line = line.Substring(0, line.Length - 1);//removing the last '>'

            var fields = line.OptimizedSplit(',');

            string type = null, json = null;
            int? index = null;

            foreach (var field in fields)
            {
                (string key, string value) = field.OptimizedKeyValue();
                if (value == null) throw new InvalidDataException("Invalid info field: " + field);

                switch (key.ToUpper())
                {
                    case "INFO":
                        break;
                    case "TYPE":
                        type = value;
                        break;
                    case "JSON":
                        json = value;
                        break;
                    case "INDEX":
                        index = Convert.ToInt16(value);
                        break;
                    default:
                        throw new InvalidDataException("Unknown field in info field line :\n" + line);
                }
            }

            if (type == null || json == null || index == null)
                throw new InvalidDataException("Missing mandatory field from IAE_INFO:\n" + line);

            if (type.ToLower() == "string")
            {
                _stringFields.Add(json);
            }
            else
            {
                _nonstringFields.Add(json);
            }
            if (_fieldIndex.ContainsKey(index.Value))
            {
                throw new InvalidDataException("duplicate index:\n" + line);
            }
            _fieldIndex[index.Value] = json;
        }
    }
}
