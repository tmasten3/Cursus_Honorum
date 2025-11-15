using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Data.Characters
{
    internal sealed class RomanPraenomenOption
    {
        public string Name { get; }
        public int Weight { get; }

        public RomanPraenomenOption(string name, int weight = 1)
        {
            Name = RomanNameUtility.Normalize(name);
            Weight = Math.Max(1, weight);
        }
    }

    internal sealed class RomanGensVariant
    {
        private readonly List<RomanPraenomenOption> _praenomina;
        private readonly Dictionary<string, RomanPraenomenOption> _praenomenLookup;
        private readonly List<string> _baseCognomina;
        private readonly HashSet<string> _cognomenSet;
        private readonly HashSet<string> _dynamicCognomina = new(StringComparer.OrdinalIgnoreCase);

        public RomanGensDefinition Definition { get; }
        public SocialClass SocialClass { get; }

        public RomanGensVariant(
            RomanGensDefinition definition,
            SocialClass socialClass,
            IEnumerable<RomanPraenomenOption> praenomina,
            IEnumerable<string> cognomina)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            SocialClass = socialClass;

            _praenomina = praenomina?.Where(p => p != null).ToList() ?? new List<RomanPraenomenOption>();
            _praenomenLookup = new Dictionary<string, RomanPraenomenOption>(StringComparer.OrdinalIgnoreCase);
            foreach (var option in _praenomina)
            {
                if (option == null || string.IsNullOrEmpty(option.Name))
                    continue;
                _praenomenLookup[option.Name] = option;
            }

            _baseCognomina = cognomina?
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(RomanNameUtility.Normalize)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
                ?? new List<string>();

            _cognomenSet = new HashSet<string>(_baseCognomina, StringComparer.OrdinalIgnoreCase);
        }

        public bool TryNormalizePraenomen(string value, out string normalized)
        {
            normalized = RomanNameUtility.Normalize(value);
            if (string.IsNullOrEmpty(normalized))
                return false;

            if (_praenomenLookup.TryGetValue(normalized, out var option))
            {
                normalized = option.Name;
                return true;
            }

            return false;
        }

        public string GetRandomPraenomen(Random random)
        {
            if (_praenomina.Count == 0)
                return null;

            int totalWeight = 0;
            foreach (var option in _praenomina)
                totalWeight += Math.Max(1, option.Weight);

            if (totalWeight <= 0)
                return _praenomina[0].Name;

            int roll = random.Next(totalWeight);
            foreach (var option in _praenomina)
            {
                int weight = Math.Max(1, option.Weight);
                if (roll < weight)
                    return option.Name;
                roll -= weight;
            }

            return _praenomina[^1].Name;
        }

        public IEnumerable<string> GetAvailableCognomina()
        {
            foreach (var cognomen in _baseCognomina)
                yield return cognomen;

            foreach (var cognomen in _dynamicCognomina)
                yield return cognomen;
        }

        public bool ContainsCognomen(string cognomen)
        {
            if (string.IsNullOrWhiteSpace(cognomen))
                return false;

            return _cognomenSet.Contains(RomanNameUtility.Normalize(cognomen));
        }

        public void RegisterCognomen(string cognomen)
        {
            var normalized = RomanNameUtility.Normalize(cognomen);
            if (string.IsNullOrEmpty(normalized))
                return;

            if (_cognomenSet.Add(normalized))
                _dynamicCognomina.Add(normalized);
        }

        public bool HasAnyCognomen => _baseCognomina.Count > 0 || _dynamicCognomina.Count > 0;
    }

    internal sealed class RomanGensDefinition
    {
        private readonly List<RomanGensVariant> _variants = new();

        public string Id { get; }
        public string StylizedNomen { get; }
        public string FeminineNomen { get; }

        public RomanGensDefinition(string id, string stylizedNomen)
        {
            Id = id;
            StylizedNomen = RomanNameUtility.Normalize(stylizedNomen);
            FeminineNomen = RomanNameUtility.ToFeminine(StylizedNomen);
        }

        public RomanGensDefinition AddVariant(
            SocialClass socialClass,
            IEnumerable<RomanPraenomenOption> praenomina,
            IEnumerable<string> cognomina)
        {
            var variant = new RomanGensVariant(this, socialClass, praenomina, cognomina);
            _variants.Add(variant);
            return this;
        }

        public RomanGensVariant GetVariantForClass(SocialClass socialClass)
        {
            foreach (var variant in _variants)
            {
                if (variant.SocialClass == socialClass)
                    return variant;
            }

            return _variants.FirstOrDefault();
        }

        public IReadOnlyList<RomanGensVariant> Variants => _variants;
    }

    internal sealed class RomanGensRegistry
    {
        private readonly List<RomanGensDefinition> _definitions;
        private readonly Dictionary<string, RomanGensDefinition> _byStylized;
        private readonly Dictionary<string, RomanGensDefinition> _byFeminine;

        public RomanGensRegistry(IEnumerable<RomanGensDefinition> definitions)
        {
            _definitions = definitions?.Where(d => d != null).ToList() ?? new List<RomanGensDefinition>();
            _byStylized = new Dictionary<string, RomanGensDefinition>(StringComparer.OrdinalIgnoreCase);
            _byFeminine = new Dictionary<string, RomanGensDefinition>(StringComparer.OrdinalIgnoreCase);

            foreach (var def in _definitions)
            {
                if (string.IsNullOrEmpty(def?.StylizedNomen))
                    continue;

                _byStylized[def.StylizedNomen] = def;
                if (!string.IsNullOrEmpty(def.FeminineNomen))
                    _byFeminine[def.FeminineNomen] = def;
            }
        }

        public RomanGensDefinition GetDefinition(string nomen)
        {
            if (string.IsNullOrWhiteSpace(nomen))
                return null;

            var normalized = RomanNameUtility.Normalize(nomen);
            if (string.IsNullOrEmpty(normalized))
                return null;

            if (_byStylized.TryGetValue(normalized, out var def))
                return def;

            if (_byFeminine.TryGetValue(normalized, out def))
                return def;

            return null;
        }

        public RomanGensVariant ResolveVariant(string family, SocialClass socialClass)
        {
            var definition = GetDefinition(family);
            return definition?.GetVariantForClass(socialClass);
        }

        public RomanGensVariant GetRandomVariant(SocialClass socialClass, Random random)
        {
            var candidates = new List<RomanGensVariant>();
            foreach (var definition in _definitions)
            {
                var variant = definition.GetVariantForClass(socialClass);
                if (variant != null)
                    candidates.Add(variant);
            }

            if (candidates.Count == 0)
                return null;

            return candidates[random.Next(candidates.Count)];
        }

        public string NormalizeFamily(string family)
        {
            return GetDefinition(family)?.StylizedNomen;
        }

        public IEnumerable<RomanGensDefinition> Definitions => _definitions;
    }

    internal static class RomanGensData
    {
        public static readonly IReadOnlyList<RomanGensDefinition> All;

        static RomanGensData()
        {
            var list = new List<RomanGensDefinition>
            {
                CreateAebutia(),
                CreateAemilia(),
                CreateAquillia(),
                CreateAtilia(),
                CreateClaudia(),
                CreateCloelia(),
                CreateCornelia(),
                CreateFabia(),
                CreateJulia(),
                CreateLucretia(),
                CreatePapiria(),
                CreateJunia(),
                CreateTullia(),
                CreateValeria(),
                CreateAntonia(),
                CreateCominia(),
                CreateSempronia(),
                CreatePostumia()
            };

            All = list;
        }

        private static RomanGensDefinition CreateAebutia()
        {
            var def = new RomanGensDefinition("Aebutia", "Aebutius");
            def.AddVariant(SocialClass.Patrician, new List<RomanPraenomenOption>
            {
                new RomanPraenomenOption("Titus"), new RomanPraenomenOption("Lucius"), new RomanPraenomenOption("Postumus"), new RomanPraenomenOption("Marcus"), new RomanPraenomenOption("Publius")
            }, new[] { "Helva" });
            def.AddVariant(SocialClass.Plebeian, new List<RomanPraenomenOption>
            {
                new RomanPraenomenOption("Titus"), new RomanPraenomenOption("Lucius"), new RomanPraenomenOption("Postumus"), new RomanPraenomenOption("Marcus"), new RomanPraenomenOption("Publius")
            }, new[] { "Carus", "Parrus", "Pinnius" });
            return def;
        }

        private static RomanGensDefinition CreateAemilia()
        {
            var def = new RomanGensDefinition("Aemilia", "Aemilius");
            def.AddVariant(SocialClass.Patrician, new List<RomanPraenomenOption>
            {
                new RomanPraenomenOption("Lucius"), new RomanPraenomenOption("Manius"), new RomanPraenomenOption("Marcus"), new RomanPraenomenOption("Quintus"), new RomanPraenomenOption("Mamercus")
            }, new[] { "Lepidus", "Papus", "Paullus", "Scaurus" });
            return def;
        }

        private static RomanGensDefinition CreateAquillia()
        {
            var def = new RomanGensDefinition("Aquillia", "Aquillius");
            def.AddVariant(SocialClass.Plebeian, new List<RomanPraenomenOption>
            {
                new RomanPraenomenOption("Gaius"), new RomanPraenomenOption("Lucius"), new RomanPraenomenOption("Marcus")
            }, new[] { "Corvus", "Crassus", "Florus", "Gallus", "Tuscus" });
            return def;
        }

        private static RomanGensDefinition CreateAtilia()
        {
            var def = new RomanGensDefinition("Atilia", "Atilius");
            def.AddVariant(SocialClass.Plebeian, new List<RomanPraenomenOption>
            {
                new RomanPraenomenOption("Lucius", 3), new RomanPraenomenOption("Marcus", 3), new RomanPraenomenOption("Gaius", 3), new RomanPraenomenOption("Aulus"), new RomanPraenomenOption("Sextus")
            }, new[] { "Luscus", "Regulus", "Calatinus", "Priscus" });
            return def;
        }

        private static RomanGensDefinition CreateClaudia()
        {
            var def = new RomanGensDefinition("Claudia", "Claudius");
            def.AddVariant(SocialClass.Patrician, new List<RomanPraenomenOption>
            {
                new RomanPraenomenOption("Appius"), new RomanPraenomenOption("Gaius"), new RomanPraenomenOption("Publius")
            }, new[] { "Caecus", "Caudex", "Centho", "Crassus", "Nero", "Pulcher" });
            def.AddVariant(SocialClass.Plebeian, new List<RomanPraenomenOption>
            {
                new RomanPraenomenOption("Marcus"), new RomanPraenomenOption("Quintus"), new RomanPraenomenOption("Sextus"), new RomanPraenomenOption("Gaius")
            }, new[] { "Marcellus" });
            return def;
        }

        private static RomanGensDefinition CreateCloelia()
        {
            var def = new RomanGensDefinition("Cloelia", "Cloelius");
            def.AddVariant(SocialClass.Patrician, new List<RomanPraenomenOption>
            {
                new RomanPraenomenOption("Titus"), new RomanPraenomenOption("Quintus"), new RomanPraenomenOption("Publius")
            }, new[] { "Siculus" });
            return def;
        }

        private static RomanGensDefinition CreateCornelia()
        {
            var def = new RomanGensDefinition("Cornelia", "Cornelius");
            def.AddVariant(SocialClass.Patrician, new List<RomanPraenomenOption>
            {
                new RomanPraenomenOption("Servius", 3), new RomanPraenomenOption("Lucius", 3), new RomanPraenomenOption("Publius", 3), new RomanPraenomenOption("Gnaeus", 3),
                new RomanPraenomenOption("Gaius"), new RomanPraenomenOption("Marcus"), new RomanPraenomenOption("Aulus")
            }, new[]
            {
                "Scipio", "Lentulus", "Cossus", "Rufinus", "Dolabella", "Merenda", "Cethegus",
                "Mammula", "Merula", "Sisenna", "Cinna", "Balbus"
            });
            return def;
        }

        private static RomanGensDefinition CreateFabia()
        {
            var def = new RomanGensDefinition("Fabia", "Fabius");
            def.AddVariant(SocialClass.Patrician, new List<RomanPraenomenOption>
            {
                new RomanPraenomenOption("Quintus", 4), new RomanPraenomenOption("Marcus"), new RomanPraenomenOption("Gaius"), new RomanPraenomenOption("Pallus"), new RomanPraenomenOption("Numerius")
            }, new[] { "Maximus", "Pictor", "Buteo", "Labeo" });
            return def;
        }

        private static RomanGensDefinition CreateJulia()
        {
            var def = new RomanGensDefinition("Julia", "Julius");
            def.AddVariant(SocialClass.Patrician, new List<RomanPraenomenOption>
            {
                new RomanPraenomenOption("Lucius"), new RomanPraenomenOption("Gaius"), new RomanPraenomenOption("Sextus")
            }, new[] { "Libo", "Caesar" });
            return def;
        }

        private static RomanGensDefinition CreateLucretia()
        {
            var def = new RomanGensDefinition("Lucretia", "Lucretius");
            def.AddVariant(SocialClass.Patrician, new List<RomanPraenomenOption>
            {
                new RomanPraenomenOption("Titus"), new RomanPraenomenOption("Spurius"), new RomanPraenomenOption("Lucius"), new RomanPraenomenOption("Publius"), new RomanPraenomenOption("Hostus")
            }, new[] { "Tricipitinus" });
            def.AddVariant(SocialClass.Plebeian, new List<RomanPraenomenOption>
            {
                new RomanPraenomenOption("Lucius", 3), new RomanPraenomenOption("Marcus", 3), new RomanPraenomenOption("Spurius", 3), new RomanPraenomenOption("Quintus", 3), new RomanPraenomenOption("Gaius"), new RomanPraenomenOption("Gnaeus"), new RomanPraenomenOption("Titus")
            }, new[] { "Gallus", "Ofella", "Vespillo" });
            return def;
        }

        private static RomanGensDefinition CreatePapiria()
        {
            var def = new RomanGensDefinition("Papiria", "Papirius");
            def.AddVariant(SocialClass.Patrician, new List<RomanPraenomenOption>
            {
                new RomanPraenomenOption("Lucius", 3), new RomanPraenomenOption("Marcus", 3), new RomanPraenomenOption("Gaius", 3), new RomanPraenomenOption("Manius", 3), new RomanPraenomenOption("Spurius", 3), new RomanPraenomenOption("Publius")
            }, new[] { "Crassus", "Cursor", "Maso", "Mugillanus" });
            def.AddVariant(SocialClass.Plebeian, new List<RomanPraenomenOption>
            {
                new RomanPraenomenOption("Gaius"), new RomanPraenomenOption("Gnaeus")
            }, new[] { "Carbo", "Paetus", "Turdus" });
            return def;
        }

        private static RomanGensDefinition CreateJunia()
        {
            var def = new RomanGensDefinition("Junia", "Junius");
            def.AddVariant(SocialClass.Plebeian, new List<RomanPraenomenOption>
            {
                new RomanPraenomenOption("Marcus", 3), new RomanPraenomenOption("Lucius", 3), new RomanPraenomenOption("Decimus", 3), new RomanPraenomenOption("Gaius"), new RomanPraenomenOption("Quintus")
            }, new[] { "Brutus", "Bubulcus", "Gracchanus", "Paciaecus", "Pennus", "Pera", "Pullus", "Silanus" });
            return def;
        }

        private static RomanGensDefinition CreateTullia()
        {
            var def = new RomanGensDefinition("Tullia", "Tullus");
            def.AddVariant(SocialClass.Plebeian, new List<RomanPraenomenOption>
            {
                new RomanPraenomenOption("Marcus", 3), new RomanPraenomenOption("Lucius", 3), new RomanPraenomenOption("Quintus")
            }, new[] { "Decula", "Cicero" });
            return def;
        }

        private static RomanGensDefinition CreateValeria()
        {
            var def = new RomanGensDefinition("Valeria", "Valerius");
            def.AddVariant(SocialClass.Patrician, new List<RomanPraenomenOption>
            {
                new RomanPraenomenOption("Publius", 3), new RomanPraenomenOption("Marcus", 3), new RomanPraenomenOption("Manius", 3), new RomanPraenomenOption("Lucius", 3), new RomanPraenomenOption("Gaius"), new RomanPraenomenOption("Quintus")
            }, new[] { "Laevinus", "Flaccus", "Messalla", "Falto" });
            return def;
        }

        private static RomanGensDefinition CreateAntonia()
        {
            var def = new RomanGensDefinition("Antonia", "Antonius");
            def.AddVariant(SocialClass.Plebeian, new List<RomanPraenomenOption>
            {
                new RomanPraenomenOption("Quintus"), new RomanPraenomenOption("Marcus"), new RomanPraenomenOption("Lucius"), new RomanPraenomenOption("Gaius")
            }, Array.Empty<string>());
            return def;
        }

        private static RomanGensDefinition CreateCominia()
        {
            var def = new RomanGensDefinition("Cominia", "Cominius");
            def.AddVariant(SocialClass.Plebeian, new List<RomanPraenomenOption>
            {
                new RomanPraenomenOption("Lucius", 3), new RomanPraenomenOption("Publius", 3), new RomanPraenomenOption("Gaius", 3), new RomanPraenomenOption("Marcus"), new RomanPraenomenOption("Quintus"), new RomanPraenomenOption("Sextus")
            }, Array.Empty<string>());
            return def;
        }

        private static RomanGensDefinition CreateSempronia()
        {
            var def = new RomanGensDefinition("Sempronia", "Sempronius");
            def.AddVariant(SocialClass.Plebeian, new List<RomanPraenomenOption>
            {
                new RomanPraenomenOption("Gaius"), new RomanPraenomenOption("Publius"), new RomanPraenomenOption("Tiberius"), new RomanPraenomenOption("Marcus")
            }, new[] { "Blaesus", "Tuditanus", "Gracchus", "Longus", "Rutilus" });
            return def;
        }

        private static RomanGensDefinition CreatePostumia()
        {
            var def = new RomanGensDefinition("Postumia", "Postumius");
            def.AddVariant(SocialClass.Patrician, new List<RomanPraenomenOption>
            {
                new RomanPraenomenOption("Aulus", 3), new RomanPraenomenOption("Spurius", 3), new RomanPraenomenOption("Lucius", 3),
                new RomanPraenomenOption("Marcus"), new RomanPraenomenOption("Publius"), new RomanPraenomenOption("Quintus"), new RomanPraenomenOption("Gaius"), new RomanPraenomenOption("Gnaeus"), new RomanPraenomenOption("Titus")
            }, new[] { "Albinus", "Megellus" });
            return def;
        }
    }
}
