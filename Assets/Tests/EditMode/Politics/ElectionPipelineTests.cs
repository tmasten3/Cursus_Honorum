using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Data.Characters;
using Game.Systems.EventBus;
using Game.Systems.Politics.Elections;
using Game.Systems.Politics.Offices;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Politics
{
    public class ElectionPipelineTests
    {
        [Test]
        public void Pipeline_Selects_And_Assigns_Winner_Deterministically()
        {
            var eventBus = new EventBus();
            var officeDefinition = new OfficeDefinition
            {
                Id = "consul",
                Name = "Consul",
                Assembly = OfficeAssembly.ComitiaCenturiata,
                MinAge = 42,
                TermLengthYears = 1,
                Seats = 1,
                ReelectionGapYears = 10,
                Rank = 5
            };

            var state = new OfficeStateService(null);
            state.EnsureSeatStructures(new[] { officeDefinition });
            var eligibility = new OfficeEligibilityService(state);

            var rng = new ZeroRandom();
            var evaluation = new CandidateEvaluationService(eligibility, rng);
            var voteSimulator = new ElectionVoteSimulator(rng);
            var resultService = new ElectionResultService(
                (officeId, characterId, year, defer) =>
                {
                    var assignment = state.AssignOffice(officeId, characterId, year, defer,
                        id => officeDefinition.Id == id ? officeDefinition : null);
                    return assignment.Descriptor;
                },
                eventBus);

            var info = new OfficeElectionInfo
            {
                Definition = officeDefinition,
                SeatsAvailable = 1,
                Seats = new List<OfficeSeatDescriptor>
                {
                    new OfficeSeatDescriptor { OfficeId = officeDefinition.Id, SeatIndex = 0 }
                }
            };

            var characters = new[]
            {
                CreateCharacter(1, "Marcus", age: 48, influence: 70, wealth: 60, SocialClass.Patrician,
                    traits: new[] { "Ambitious", "Popular" }),
                CreateCharacter(2, "Lucius", age: 45, influence: 55, wealth: 45, SocialClass.Patrician,
                    traits: new[] { "Charismatic" }),
                CreateCharacter(3, "Gaius", age: 43, influence: 35, wealth: 30, SocialClass.Plebeian)
            };

            var declarations = new List<CandidateDeclaration>();
            foreach (var character in characters)
            {
                Assert.IsTrue(evaluation.TryCreateDeclaration(character, new[] { info }, 300, out var declaration));
                declarations.Add(declaration);
            }

            var candidates = new List<ElectionCandidate>();
            foreach (var declaration in declarations)
            {
                var character = characters.Single(c => c.ID == declaration.CharacterId);
                Assert.IsTrue(evaluation.IsEligible(character, officeDefinition, 300));
                candidates.Add(new ElectionCandidate
                {
                    Declaration = declaration,
                    Character = character,
                    VoteBreakdown = new Dictionary<string, float>(declaration.Factors)
                });
            }

            voteSimulator.ScoreCandidates(officeDefinition, candidates);

            var ordered = candidates
                .OrderByDescending(c => c.FinalScore)
                .Select(c => c.Character.ID)
                .ToList();

            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, ordered);

            var winners = voteSimulator.SelectWinners(candidates, info.SeatsAvailable);
            Assert.AreEqual(1, winners.Count);
            Assert.AreEqual(1, winners[0].Character.ID);

            float totalScore = candidates.Sum(c => Mathf.Max(0.1f, c.FinalScore));
            var (summary, record) = resultService.ApplyOfficeResults(officeDefinition, 300, candidates, winners,
                totalScore, debugMode: false, logInfo: _ => { }, logWarn: _ => Assert.Fail("Unexpected warning"));

            Assert.AreEqual(1, summary.Winners.Count);
            Assert.AreEqual(1, summary.Winners[0].CharacterId);
            Assert.AreEqual(0, summary.Winners[0].SeatIndex);

            var holdings = state.GetCurrentHoldings(1);
            Assert.That(holdings.Count, Is.EqualTo(1));
            Assert.That(holdings[0].OfficeId, Is.EqualTo("consul"));

            ElectionSeasonCompletedEvent publishedEvent = null;
            eventBus.Subscribe<ElectionSeasonCompletedEvent>(e => publishedEvent = e);

            resultService.PublishElectionResults(300, 7, 1, new[] { summary });

            Assert.IsNotNull(publishedEvent);
            Assert.AreEqual(300, publishedEvent.ElectionYear);
            Assert.AreEqual(1, publishedEvent.Results.Count);
            Assert.AreEqual("consul", publishedEvent.Results[0].OfficeId);
        }

        private static Character CreateCharacter(int id, string name, int age, int influence, int wealth, SocialClass socialClass,
            IEnumerable<string> traits = null)
        {
            return new Character
            {
                ID = id,
                RomanName = new RomanName(name, "Test", null, Gender.Male),
                Gender = Gender.Male,
                Age = age,
                Influence = influence,
                Wealth = wealth,
                Class = socialClass,
                Traits = traits?.ToList() ?? new List<string>(),
                IsAlive = true
            };
        }

        private class ZeroRandom : System.Random
        {
            protected override double Sample() => 0.0;
        }
    }
}
