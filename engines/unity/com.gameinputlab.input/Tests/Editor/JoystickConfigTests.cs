using System;
using NUnit.Framework;
using UnityEngine;

namespace GameInputLab.Tests
{
    public class JoystickConfigTests
    {
        [Test]
        public void BareConfigJsonKeepsDefaultsForMissingFields()
        {
            JoystickConfig config = JoystickConfig.FromJson("{\"mode\":\"floating\",\"radius\":72}");
            Assert.IsTrue(config.floating);
            Assert.AreEqual(72f, config.radius);
            Assert.AreEqual(10f, config.deadZone);
            Assert.AreEqual(1f, config.curve);
        }

        [Test]
        public void OutOfRangeValuesAreClampedAndRoundedLikeTheApp()
        {
            JoystickConfig config = JoystickConfig.FromJson("{\"radius\":999,\"deadZone\":-5,\"curve\":1.23456,\"sensitivity\":1.234}");
            Assert.AreEqual(96f, config.radius);
            Assert.AreEqual(0f, config.deadZone);
            Assert.AreEqual(1.2f, config.curve, 1e-6f);
            Assert.AreEqual(1.23f, config.sensitivity, 1e-6f);
        }

        [Test]
        public void ForeignFormatIsRejected()
        {
            Assert.Throws<ArgumentException>(() => JoystickConfig.FromJson("{\"format\":\"something-else\",\"config\":{}}"));
            Assert.Throws<ArgumentException>(() => JoystickConfig.FromJson("  "));
        }

        [Test]
        public void NormalizedReplacesInvalidNumbersWithDefaults()
        {
            var config = new JoystickConfig { radius = float.NaN, smoothing = float.PositiveInfinity }.Normalized();
            Assert.AreEqual(64f, config.radius);
            Assert.AreEqual(0f, config.smoothing);
        }

        [Test]
        public void ReleaseEventReportsTheReason()
        {
            var tracker = new StickTracker { Area = new Rect(0, 0, 100, 100) };
            StickReleaseReason? reported = null;
            tracker.Released += reason => reported = reason;
            tracker.Step(new[] { new StickTouch(1, new Vector2(50, 50), StickTouchPhase.Began) }, 16);
            tracker.Step(new[] { new StickTouch(1, new Vector2(80, 50), StickTouchPhase.Canceled) }, 16);
            Assert.AreEqual(StickReleaseReason.Canceled, reported);
            Assert.AreEqual(Vector2.zero, tracker.Output);
        }
    }
}
