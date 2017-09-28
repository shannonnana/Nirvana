﻿using System.IO;
using System.Text;
using VariantAnnotation.AnnotatedPositions.Transcript;
using VariantAnnotation.Caches.DataStructures;
using VariantAnnotation.Interface.AnnotatedPositions;
using VariantAnnotation.IO;
using Xunit;

namespace UnitTests.VariantAnnotation.Caches.DataStructures
{
    public sealed class TranslationTests
    {
        [Fact]
        public void Translation_EndToEnd()
        {
            ICdnaCoordinateMap expectedCodingRegion = new CdnaCoordinateMap(100, 200, 300, 400);
            string expectedProteinId                = "ENSP00000446475";
            byte expectedProteinVersion             = 7;
            string expectedPeptideSeq               = "VEIDSD";

            string[] peptideSeqs = { expectedPeptideSeq };

            ITranslation expectedTranslation =
                new Translation(expectedCodingRegion, CompactId.Convert(expectedProteinId), expectedProteinVersion,
                    expectedPeptideSeq);

            ITranslation observedTranslation;

            using (var ms = new MemoryStream())
            {
                using (var writer = new ExtendedBinaryWriter(ms, Encoding.UTF8, true))
                {
                    expectedTranslation.Write(writer, 0);
                }

                ms.Position = 0;

                using (var reader = new ExtendedBinaryReader(ms))
                {
                    observedTranslation = Translation.Read(reader, peptideSeqs);
                }
            }

            Assert.NotNull(observedTranslation);
            Assert.Equal(expectedCodingRegion.CdnaStart, observedTranslation.CodingRegion.CdnaStart);
            Assert.Equal(expectedProteinId,              observedTranslation.ProteinId.ToString());
            Assert.Equal(expectedProteinVersion,         observedTranslation.ProteinVersion);
            Assert.Equal(expectedPeptideSeq,             observedTranslation.PeptideSeq);
        }
    }
}