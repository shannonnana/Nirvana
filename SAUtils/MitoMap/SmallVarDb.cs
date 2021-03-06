﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommandLine.Builders;
using CommandLine.NDesk.Options;
using ErrorHandling;
using IO;
using SAUtils.InputFileParsers;
using VariantAnnotation.Providers;
using VariantAnnotation.SA;

namespace SAUtils.MitoMap
{
    public static class SmallVarDb
    {
        private static string _compressedReference;
        private static string _outputDirectory;
        private static readonly List<string> MitoMapFileNames = new List<string>();
        private static string _mitoMapDatabase;

        public static ExitCodes Run(string command, string[] commandArgs)
        {
            var ops = new OptionSet
            {
                {
                    "ref|r=",
                    "compressed reference sequence file",
                    v => _compressedReference = v
                },
                {
                    "in|i=",
                    "MITOMAP small variants HTML file",
                    v => MitoMapFileNames.Add(v)
                },
                {
                    "database|d=",
                    "MITOMAP database",
                    v => _mitoMapDatabase = v
                },
                {
                    "out|o=",
                    "output directory",
                    v => _outputDirectory = v
                }
            };

            string commandLineExample = $"{command} [options]";

            var exitCode = new ConsoleAppBuilder(commandArgs, ops)
                .Parse()
                .CheckInputFilenameExists(_compressedReference, "compressed reference sequence file name", "--ref")
                .CheckEachFilenameExists(MitoMapFileNames, "MITOMAP small variants HTML file", "--in")
                .CheckInputFilenameExists(_mitoMapDatabase, "output directory", "--database")
                .CheckDirectoryExists(_outputDirectory, "output directory", "--out")
                .SkipBanner()
                .ShowHelpMenu("Creates a supplementary database with MITOMAP small variants annotations", commandLineExample)
                .ShowErrors()
                .Execute(ProgramExecution);

            return exitCode;
        }
        private static ExitCodes ProgramExecution()
        {

            var rootDirectory = new FileInfo(MitoMapFileNames[0]).Directory;
            if (rootDirectory == null) return ExitCodes.PathNotFound;
            var version = DataSourceVersionReader.GetSourceVersion(Path.Combine(rootDirectory.ToString(), "mitoMapVar"));
            var sequenceProvider = new ReferenceSequenceProvider(FileUtilities.GetReadStream(_compressedReference));
            var chrom = sequenceProvider.RefNameToChromosome["chrM"];
            sequenceProvider.LoadChromosome(chrom);
            MitoMapInputDb mitoMapInputDb = MitoMapDatabaseUtilities.Create(_mitoMapDatabase);
            var mitoMapVarReaders = MitoMapFileNames.Select(mitoMapFileName => new MitoMapVariantReader(new FileInfo(mitoMapFileName), mitoMapInputDb, sequenceProvider)).ToList();
            var mergedMitoMapVarItems = MitoMapVariantReader.GetMergeAndSortedItems(mitoMapVarReaders);

            string outFileName = $"{version.Name}_{version.Version}";
            using (var nsaStream   = FileUtilities.GetCreateStream(Path.Combine(_outputDirectory, outFileName + SaCommon.SaFileSuffix)))
            using (var indexStream = FileUtilities.GetCreateStream(Path.Combine(_outputDirectory, outFileName + SaCommon.SaFileSuffix + SaCommon.IndexSufix)))
            using (var nsaWriter   = new NsaWriter(nsaStream, indexStream, version, sequenceProvider, SaCommon.MitoMapTag, false, true, SaCommon.SchemaVersion, false))
            {
                nsaWriter.Write(mergedMitoMapVarItems);
            }

            return ExitCodes.Success;
        }
    }
}