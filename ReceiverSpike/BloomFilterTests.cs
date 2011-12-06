using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace ReceiverSpike
{
	public class BloomFilterTests
	{
		const float PointOnePerCent = 0.001F;
		const int TenThousand = 10000;
		const float PointZeroOnePerCent = 0.0001F;
		const int OneMillion = 1000000;
		const float OnePerCent = 0.01F;
		const int TwoMillion = 2000000;

		/// <summary>
		/// There should be no false negatives.
		/// </summary>
		[Test]
		public void NoFalseNegativesTest()
		{
			// create input collection
			var inputs = GenerateRandomDataList(TenThousand);

			// instantiate filter and populate it with the inputs
			Filter<string> target = new BloomFilter<string>(TenThousand, PointOnePerCent, null);
			
			foreach (var input in inputs)
				target.Add(input);

			// check for each input. if any are missing, the test failed
			foreach (var input in inputs.Where(input => target.Contains(input) == false))
				Assert.Fail("False negative: {0}", input);
		}

		/// <summary>
		/// Only in extreme cases should there be a false positive with this test.
		/// </summary>
		[Test]
		public void LowProbabilityFalseTest()
		{
			// instantiate filter and populate it with a single random value
			Filter<string> target = new BloomFilter<string>(TenThousand, PointZeroOnePerCent, null);
			target.Add(Guid.NewGuid().ToString());

			// generate a new random value and check for it
			if (target.Contains(Guid.NewGuid().ToString()))
				Assert.Fail("Check for missing item returned true.");
		}

		[Test]
		public void FalsePositivesInRangeTest()
		{
			// instantiate filter and populate it with random strings
			Filter<string> target = new BloomFilter<string>(OneMillion, PointOnePerCent, null);
			
			for (var i = 0; i < OneMillion; i++)
				target.Add(Guid.NewGuid().ToString());

			// generate new random strings and check for them
			// about errorRate of them should return positive
			var falsePositives = 0;
			const uint expectedFalsePositives = ((uint)(OneMillion * PointOnePerCent)) * 2;

			for (int i = 0; i < OneMillion; i++)
			{
				var test = Guid.NewGuid().ToString();

				if (target.Contains(test))
					falsePositives++;
			}

			if (falsePositives > expectedFalsePositives)
				Assert.Fail("Number of false positives ({0}) greater than expected ({1}).", falsePositives, expectedFalsePositives);
		}

		[Test]
		[ExpectedException(typeof(ArgumentOutOfRangeException))]
		public void OverLargeInputTest()
		{
			// set filter properties
			var capacity = int.MaxValue - 1;
			var errorRate = OnePerCent; // 1%

			// instantiate filter
			Filter<string> target = new BloomFilter<string>(capacity, errorRate, null);
		}

		[Test]
		public void LargeInputTest()
		{
			// instantiate filter and populate it with random strings
			Filter<string> target = new BloomFilter<string>(TwoMillion, OnePerCent, null);
			for (var i = 0; i < TwoMillion; i++)
				target.Add(Guid.NewGuid().ToString());

			// if it didn't error out on that much input, this test succeeded
		}

		[Test]
		public void LargeInputTestAutoError()
		{
			// instantiate filter and populate it with random strings
			Filter<string> target = new BloomFilter<string>(TwoMillion);

			for (int i = 0; i < TwoMillion; i++)
				target.Add(Guid.NewGuid().ToString());

			// if it didn't error out on that much input, this test succeeded
		}

		/// <summary>
		/// If k and m are properly choses for n and the error rate, the filter should be about half full.
		/// </summary>
		[Test]
		public void TruthinessTest()
		{
			var capacity = TenThousand;
			var errorRate = PointOnePerCent; // 0.1%
			
			Filter<string> target = new BloomFilter<string>(capacity, errorRate, null);
			
			for (var i = 0; i < capacity; i++)
				target.Add(Guid.NewGuid().ToString());

			double actual = target.Truthiness;
			double expected = 0.5;
			double threshold = 0.01; // filter shouldn't be < 49% or > 51% "true"
			Assert.IsTrue(Math.Abs(actual - expected) < threshold, "Information density too high or low. Actual={0}, Expected={1}", actual, expected);
		}

		private static List<String> GenerateRandomDataList(uint capacity)
		{
			var inputs = new List<string>((int)capacity);
			
			for (int i = 0; i < capacity; i++)
				inputs.Add(Guid.NewGuid().ToString());
			
			return inputs;
		}
	}
}