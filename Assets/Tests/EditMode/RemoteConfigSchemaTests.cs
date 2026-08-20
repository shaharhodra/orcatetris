using System.Collections.Generic;
using NUnit.Framework;
using OrcaTetris.Adventure;

namespace OrcaTetris.Adventure.Tests
{
    /// <summary>
    /// Pins the Remote Config schema to the behaviour the build actually ships with.
    ///
    /// The whole design rests on one property: adding Remote Config changes nothing on its own.
    /// Every default in <see cref="RemoteConfigKeys.BuildDefaults"/> is supposed to equal the value
    /// the game already used, so a failed fetch, a throttled fetch, an offline player and an empty
    /// console all behave exactly like the build did before any of this existed. That property is
    /// invisible at a glance — it is a list of numbers in one file agreeing with a list of numbers
    /// spread across several others — and it silently rots the moment somebody tunes a constant
    /// and forgets the schema. Then the shipped default quietly disagrees with the code, and every
    /// player who fails to fetch gets different gameplay from every player who succeeds, which is
    /// close to impossible to reproduce from a bug report.
    ///
    /// Only the values reachable from this assembly are covered — the Adventure curves and the tray
    /// selection knobs. Scene-serialized fields (scoring, gifts, revive) live on MonoBehaviours in
    /// Assembly-CSharp, which an asmdef test assembly cannot reference; those defaults were read
    /// out of gameScene.unity by hand and are checked in the editor instead.
    /// </summary>
    public class RemoteConfigSchemaTests
    {
        private static Dictionary<string, object> Defaults => RemoteConfigKeys.BuildDefaults();

        [Test]
        public void AdventureCurveDefaultsMatchShippedValues()
        {
            var defaults = Defaults;

            AssertLong(defaults, RemoteConfigKeys.AdvFirstGeneratedLevel, AdventureLevelCurves.FirstGeneratedLevel);
            AssertLong(defaults, RemoteConfigKeys.AdvSizeSaturationLevel, AdventureLevelCurves.SaturationLevel);
            AssertLong(defaults, RemoteConfigKeys.AdvDifficultySaturationLevel, AdventureLevelCurves.DifficultySaturationLevel);
            AssertLong(defaults, RemoteConfigKeys.AdvDifficultyCeiling, AdventureLevelCurves.DifficultyCeiling);
            AssertLong(defaults, RemoteConfigKeys.AdvInLevelDifficultyPeak, AdventureLevelCurves.InLevelDifficultyPeak);
            AssertLong(defaults, RemoteConfigKeys.AdvTargetFloor, AdventureLevelCurves.TargetFloor);
            AssertLong(defaults, RemoteConfigKeys.AdvTargetCeiling, AdventureLevelCurves.TargetCeiling);
            AssertLong(defaults, RemoteConfigKeys.AdvFillFloor, AdventureLevelCurves.FillFloor);
            AssertLong(defaults, RemoteConfigKeys.AdvFillCeiling, AdventureLevelCurves.FillCeiling);
            AssertLong(defaults, RemoteConfigKeys.AdvMinPerType, AdventureLevelCurves.MinPerType);
        }

        [Test]
        public void TraySelectionDefaultsMatchShippedValues()
        {
            var defaults = Defaults;

            AssertLong(defaults, RemoteConfigKeys.TrayMinSafeAnchors, TraySelectionCore.MinSafeAnchors);
            AssertLong(defaults, RemoteConfigKeys.TrayRobustAnchorCap, TraySelectionCore.RobustAnchorCap);

            Assert.IsTrue(defaults.TryGetValue(RemoteConfigKeys.TrayJackpotReliefFill, out var relief),
                $"Schema is missing '{RemoteConfigKeys.TrayJackpotReliefFill}'.");
            Assert.AreEqual(TraySelectionCore.JackpotCapReliefFill, System.Convert.ToDouble(relief), 1e-6,
                $"Default for '{RemoteConfigKeys.TrayJackpotReliefFill}' disagrees with the shipped value.");
        }

        /// <summary>
        /// Ad unit IDs default to Google's test units on purpose — internal builds must not run
        /// traffic through the production units, which is how an AdMob account gets flagged for
        /// invalid activity. Going live is a console change. If this test ever fails because the
        /// defaults were switched to production IDs, that is a decision to make deliberately and
        /// with the console configured, not a stale assertion to update.
        /// </summary>
        [Test]
        public void AdUnitIdsDefaultToTestUnits()
        {
            var defaults = Defaults;

            Assert.AreEqual(RemoteConfigKeys.TestInterstitialUnitId, defaults[RemoteConfigKeys.AdsInterstitialUnitIdAndroid]);
            Assert.AreEqual(RemoteConfigKeys.TestRewardedUnitId, defaults[RemoteConfigKeys.AdsRewardedUnitIdAndroid]);
            Assert.AreEqual(RemoteConfigKeys.TestInterstitialUnitId, defaults[RemoteConfigKeys.AdsInterstitialUnitIdIos]);
            Assert.AreEqual(RemoteConfigKeys.TestRewardedUnitId, defaults[RemoteConfigKeys.AdsRewardedUnitIdIos]);
        }

        /// <summary>
        /// Ad frequency gating defaults to off. Turning it on is an improvement, but it must be an
        /// opt-in one: shipping a cap by default would silently change monetization at the same
        /// moment the config system landed, making the two impossible to tell apart in the numbers.
        /// </summary>
        [Test]
        public void AdFrequencyGatingDefaultsToDisabled()
        {
            var defaults = Defaults;

            AssertLong(defaults, RemoteConfigKeys.AdsInterstitialCooldownSeconds, 0);
            AssertLong(defaults, RemoteConfigKeys.AdsInterstitialMinLevelEnds, 0);
            Assert.AreEqual(true, defaults[RemoteConfigKeys.AdsEnabled]);
        }

        /// <summary>
        /// Every key the game can read must be declared, because GameSettings resolves reads against
        /// this dictionary — an undeclared key returns zero rather than the intended value, which
        /// would show up as a difficulty or payout of 0 rather than as an error anybody notices.
        /// </summary>
        [Test]
        public void EveryDeclaredKeyHasADefault()
        {
            var defaults = Defaults;

            foreach (var field in typeof(RemoteConfigKeys).GetFields(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                if (field.FieldType != typeof(string))
                    continue;

                var keyName = (string)field.GetValue(null);

                // The test ad unit IDs are values, not keys.
                if (field.Name == nameof(RemoteConfigKeys.TestInterstitialUnitId) ||
                    field.Name == nameof(RemoteConfigKeys.TestRewardedUnitId))
                    continue;

                Assert.IsTrue(defaults.ContainsKey(keyName),
                    $"RemoteConfigKeys.{field.Name} declares key '{keyName}' but BuildDefaults has no entry for it. " +
                    "GameSettings would resolve it to zero.");
            }
        }

        private static void AssertLong(Dictionary<string, object> defaults, string key, long expected)
        {
            Assert.IsTrue(defaults.TryGetValue(key, out var value), $"Schema is missing '{key}'.");
            Assert.AreEqual(expected, System.Convert.ToInt64(value),
                $"Default for '{key}' disagrees with the shipped value — the schema and the code have drifted.");
        }
    }
}
