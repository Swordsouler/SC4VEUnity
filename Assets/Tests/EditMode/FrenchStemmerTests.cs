using NUnit.Framework;
using Sc4ve.Multimodality.Intent.RuleBased;

namespace Sc4ve.Tests.EditMode
{
    public class FrenchStemmerTests
    {
        // Cas documentés dans FrenchStemmer : « coloris », « colorie », « colorisez »
        // produisent tous le stem « color » ; « déplaça » (passé simple produit par
        // le STT pour « déplace ça ») produit « deplac ».
        [TestCase("colorie", "color")]
        [TestCase("coloris", "color")]
        [TestCase("colorisez", "color")]
        [TestCase("déplaça", "deplac")]
        public void Stem_NormalizesDocumentedVerbForms(string word, string expected)
        {
            Assert.AreEqual(expected, FrenchStemmer.Stem(word));
        }

        [Test]
        public void Stem_CollapsesConjugationsToSameStem()
        {
            string reference = FrenchStemmer.Stem("agrandis");
            Assert.AreEqual(reference, FrenchStemmer.Stem("agrandir"));
            Assert.AreEqual(reference, FrenchStemmer.Stem("agrandissez"));
        }

        [Test]
        public void NormalizeAccents_StripsFrenchDiacritics()
        {
            Assert.AreEqual("deplace ca a cote", FrenchStemmer.NormalizeAccents("déplacé çà à côté"));
        }

        [Test]
        public void Stem_IsIdempotent()
        {
            foreach (string word in new[] { "colorie", "agrandissez", "pommes", "sélectionne" })
            {
                string once = FrenchStemmer.Stem(word);
                Assert.AreEqual(once, FrenchStemmer.Stem(once), $"Stem non idempotent pour « {word} »");
            }
        }
    }
}
