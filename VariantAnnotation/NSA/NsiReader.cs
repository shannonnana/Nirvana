﻿using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Compression.Algorithms;
using Compression.FileHandling;
using ErrorHandling.Exceptions;
using Genome;
using Intervals;
using IO;
using VariantAnnotation.Interface.Providers;
using VariantAnnotation.Interface.SA;
using VariantAnnotation.IO;
using VariantAnnotation.Providers;
using VariantAnnotation.SA;
using Variants;

namespace VariantAnnotation.NSA
{
    public sealed class NsiReader : INsiReader
    {
        public GenomeAssembly Assembly { get; }
        public IDataSourceVersion Version { get; }
        public string JsonKey { get; }
        public ReportFor ReportFor { get; }
        private readonly Dictionary<ushort, IntervalArray<string>> _intervalArrays;

        private NsiReader(GenomeAssembly assembly, IDataSourceVersion version, string jsonKey, ReportFor reportFor, Dictionary<ushort, IntervalArray<string>> intervalArrays)
        {
            Assembly        = assembly;
            Version         = version;
            JsonKey         = jsonKey;
            ReportFor       = reportFor;
            _intervalArrays = intervalArrays;
        }

        public static NsiReader Read(Stream stream)
        {
            (IDataSourceVersion version, GenomeAssembly assembly, string jsonKey, ReportFor reportFor, int schemaVersion) = ReadHeader(stream);
            if (schemaVersion != SaCommon.SchemaVersion)
                throw new UserErrorException($"Schema version mismatch!! Expected {SaCommon.SchemaVersion}, observed {schemaVersion} for {jsonKey}");

            using (var blockStream = new BlockStream(new Zstandard(), stream, CompressionMode.Decompress))
            using (var reader = new ExtendedBinaryReader(blockStream))
            {
                int count = reader.ReadOptInt32();
                var suppIntervals = new Dictionary<ushort, List<Interval<string>>>();
                for (var i = 0; i < count; i++)
                {
                    var saInterval = SuppInterval.Read(reader);
                    if (suppIntervals.TryGetValue(saInterval.Chromosome.Index, out var intervals)) intervals.Add(new Interval<string>(saInterval.Start, saInterval.End, saInterval.GetJsonString()));
                    else suppIntervals[saInterval.Chromosome.Index] = new List<Interval<string>> { new Interval<string>(saInterval.Start, saInterval.End, saInterval.GetJsonString()) };
                }

                var intervalArrays = new Dictionary<ushort, IntervalArray<string>>(suppIntervals.Count);
                foreach ((ushort chromIndex, List<Interval<string>> intervals) in suppIntervals)
                {
                    intervalArrays[chromIndex] = new IntervalArray<string>(intervals.ToArray());
                }

                return new NsiReader(assembly, version, jsonKey, reportFor, intervalArrays);
            }
            
        }

        private static (IDataSourceVersion, GenomeAssembly, string, ReportFor, int) ReadHeader(Stream stream)
        {

            using (var reader = new ExtendedBinaryReader(stream, Encoding.UTF8, true))
            {
                var identifier = reader.ReadAsciiString();
                if(identifier != SaCommon.NsiIdentifier)
                    throw new InvalidDataException($"Failed to find identifier!!Expected: {SaCommon.NsiIdentifier}, observed:{identifier}");

                var version       = DataSourceVersion.Read(reader);
                var assembly      = (GenomeAssembly)reader.ReadByte();
                var jsonKey       = reader.ReadAsciiString();
                var reportFor     = (ReportFor)reader.ReadByte();
                int schemaVersion = reader.ReadInt32();
                
                var guard = reader.ReadUInt32();
                if (guard != SaCommon.GuardInt)
                    throw new InvalidDataException($"Failed to find guard int!!Expected: {SaCommon.GuardInt}, observed:{guard}");

                return (version, assembly, jsonKey, reportFor, schemaVersion);
            }
        }

        public IEnumerable<string> GetAnnotation(IVariant variant)
        {
            if (!_intervalArrays.ContainsKey(variant.Chromosome.Index)) return null;

            var overlappingSvs = _intervalArrays[variant.Chromosome.Index]
                .GetAllOverlappingIntervals(variant.Start, variant.End);

            if (overlappingSvs == null) return null;
            
            var jsonStrings = new List<string>();
            foreach (var interval in overlappingSvs)
            {
                var (reciprocalOverlap, annotationOverlap) = SuppIntervalUtilities.GetOverlapFractions(
                    new ChromosomeInterval(variant.Chromosome, interval.Begin, interval.End), variant);
                jsonStrings.Add(AddOverlapToAnnotation(interval.Value, reciprocalOverlap, annotationOverlap));
            }

            return jsonStrings;
        }

        private static string AddOverlapToAnnotation(string jsonString, double? reciprocalOverlap, double? annotationOverlap)
        {
            if (reciprocalOverlap != null)
                jsonString+=JsonObject.Comma + "\"reciprocalOverlap\":" + reciprocalOverlap.Value.ToString("0.#####");
            if (annotationOverlap != null)
                jsonString += JsonObject.Comma + "\"annotationOverlap\":" + annotationOverlap.Value.ToString("0.#####");
            return jsonString;
        }

        
    }
}