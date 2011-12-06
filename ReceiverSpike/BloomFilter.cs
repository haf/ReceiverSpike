using System;
using System.Collections;
using System.Diagnostics.Contracts;
using System.Linq;

namespace ReceiverSpike
{
	public interface Filter<in T>
	{
		/// <summary>
		/// 	Adds a new item to the filter. It cannot be removed.
		/// </summary>
		/// <param name = "item"></param>
		void Add(T item);

		/// <summary>
		/// 	Checks for the existance of the item in the filter for a given probability.
		/// </summary>
		/// <param name = "item"></param>
		/// <returns></returns>
		bool Contains(T item);

		/// <summary>
		/// 	The ratio of false to true bits in the filter. E.g., 1 true bit in a 10 bit filter means a truthiness of 0.1.
		/// </summary>
		double Truthiness { get; }
	}

	public class BloomFilter<T> : Filter<T>
	{
		/// <summary>
		/// 	A function that can be used to hash input.
		/// </summary>
		/// <param name = "input">The values to be hashed.</param>
		/// <returns>The resulting hash code.</returns>
		public delegate int HashFunction(T input);

		/// <summary>
		/// 	Creates a new Bloom filter, using the optimal size for the underlying data structure based on the desired capacity and error rate, as well as the optimal number of hash functions.
		/// 	A secondary hash function will be provided for you if your type T is either string or int. Otherwise an exception will be thrown. If you are not using these types please use the overload that supports custom hash functions.
		/// </summary>
		/// <param name = "capacity">The anticipated number of items to be added to the filter. More than this number of items can be added, but the error rate will exceed what is expected.</param>
		/// <param name = "errorRate">The accepable false-positive rate (e.g., 0.01F = 1%)</param>
		public BloomFilter(int capacity, int errorRate) : this(capacity, errorRate, null)
		{
			Contract.Requires(capacity > 0);
		}

		/// <summary>
		/// 	Creates a new Bloom filter, specifying an error rate of 1/capacity, using the optimal size for the underlying data structure based on the desired capacity and error rate, as well as the optimal number of hash functions.
		/// </summary>
		/// <param name = "capacity">The anticipated number of items to be added to the filter. More than this number of items can be added, but the error rate will exceed what is expected.</param>
		/// <param name = "hashFunction">The function to hash the input values. Do not use GetHashCode(). If it is null, and T is string or int a hash function will be provided for you.</param>
		public BloomFilter(int capacity, HashFunction hashFunction = null) : this(capacity, BestErrorRate(capacity), hashFunction)
		{
			Contract.Requires(capacity > 0);
		}

		/// <summary>
		/// 	Creates a new Bloom filter, using the optimal size for the underlying data structure based on the desired capacity and error rate, as well as the optimal number of hash functions.
		/// </summary>
		/// <param name = "capacity">The anticipated number of items to be added to the filter. More than this number of items can be added, but the error rate will exceed what is expected.</param>
		/// <param name = "errorRate">The accepable false-positive rate (e.g., 0.01F = 1%)</param>
		/// <param name = "hashFunction">The function to hash the input values. Do not use GetHashCode(). If it is null, and T is string or int a hash function will be provided for you.</param>
		public BloomFilter(int capacity, float errorRate, HashFunction hashFunction)
			: this(capacity, errorRate, hashFunction, BestM(capacity, errorRate), BestK(capacity, errorRate))
		{
			Contract.Requires(capacity > 0);
			Contract.Requires(!(errorRate >= 1.0 || errorRate <= 0.0));
		}

		/// <summary>
		/// 	Creates a new Bloom filter.
		/// </summary>
		/// <param name = "capacity">The anticipated number of items to be added to the filter. More than this number of items can be added, but the error rate will exceed what is expected.</param>
		/// <param name = "errorRate">The accepable false-positive rate (e.g., 0.01F = 1%)</param>
		/// <param name = "hashFunction">The function to hash the input values. Do not use GetHashCode(). If it is null, and T is string or int a hash function will be provided for you.</param>
		/// <param name = "m">The number of elements in the BitArray.</param>
		/// <param name = "k">The number of hash functions to use.</param>
		public BloomFilter(int capacity, float errorRate, HashFunction hashFunction, int m, int k)
		{
			Contract.Requires(capacity > 0);
			Contract.Requires(!(errorRate >= 1.0 || errorRate <= 0.0));

			if (m < 1) // from overflow in bestM calculation
				throw new ArgumentOutOfRangeException(
					String.Format(
						"The provided capacity and errorRate values would result in an array of length > int.MaxValue. Please reduce either of these values. Capacity: {0}, Error rate: {1}",
						capacity, errorRate));

			// set the secondary hash function
			if (hashFunction == null)
			{
				if (typeof (T) == typeof (String))
					_getHashSecondary = HashString;
				else if (typeof (T) == typeof (int))
					_getHashSecondary = HashInt32;
				else
					throw new ArgumentNullException("hashFunction",
					                                "Please provide a hash function for your type T, when T is not a string or int.");
			}
			else
				_getHashSecondary = hashFunction;

			_hashFunctionCount = k;
			_hashBits = new BitArray(m);
		}

		/// <summary>
		/// 	Adds a new item to the filter. It cannot be removed.
		/// </summary>
		/// <param name = "item"></param>
		public void Add(T item)
		{
			// start flipping bits for each hash of item
			var primaryHash = item.GetHashCode();
			var secondaryHash = _getHashSecondary(item);

			for (var i = 0; i < _hashFunctionCount; i++)
			{
				var hash = ComputeHash(primaryHash, secondaryHash, i);
				_hashBits[hash] = true;
			}
		}

		/// <summary>
		/// 	Checks for the existance of the item in the filter for a given probability.
		/// </summary>
		/// <param name = "item"></param>
		/// <returns></returns>
		public bool Contains(T item)
		{
			var primaryHash = item.GetHashCode();
			var secondaryHash = _getHashSecondary(item);

			for (var i = 0; i < _hashFunctionCount; i++)
			{
				var hash = ComputeHash(primaryHash, secondaryHash, i);

				if (!_hashBits[hash])
					return false;
			}

			return true;
		}

		/// <summary>
		/// 	The ratio of false to true bits in the filter. E.g., 1 true bit in a 10 bit filter means a truthiness of 0.1.
		/// </summary>
		public double Truthiness
		{
			get { return TrueBits()/(double)_hashBits.Count; }
		}

		int TrueBits()
		{
			return _hashBits.Cast<bool>().Count(bit => bit);
		}

		/// <summary>
		/// 	Performs Dillinger and Manolios double hashing.
		/// </summary>
		int ComputeHash(int primaryHash, int secondaryHash, int i)
		{
			Contract.Ensures(Contract.Result<int>() >= 0);
			var resultingHash = unchecked((primaryHash + (i*secondaryHash)) % _hashBits.Count);
			return Math.Abs(resultingHash);
		}

		readonly int _hashFunctionCount;
		readonly BitArray _hashBits;
		readonly HashFunction _getHashSecondary;

		static int BestK(int capacity, float errorRate)
		{
			var bestM = BestM(capacity, errorRate);
			var value = Math.Log(2.0)*bestM/capacity;
			return (int) Math.Round(value);
		}

		static int BestM(int capacity, float errorRate)
		{
			return (int) Math.Ceiling(capacity * Math.Log(errorRate, (1.0/Math.Pow(2.0, Math.Log(2.0)))));
		}

		static float BestErrorRate(int capacity)
		{
			Contract.Ensures(!(Contract.Result<float>() >= 1.0 || Contract.Result<float>() <= 0.0));

			var c = (float) (1.0/capacity);

			if (Math.Abs(c - 0.0) > 0.00000001)
				return c;
			
			return (float) Math.Pow(0.6185, int.MaxValue/(double)capacity);
			// http://www.cs.princeton.edu/courses/archive/spring02/cs493/lec7.pdf
		}

		/// <summary>
		/// 	Hashes a 32-bit signed int using Thomas Wang's method v3.1 (http://www.concentric.net/~Ttwang/tech/inthash.htm).
		/// 	Runtime is suggested to be 11 cycles.
		/// </summary>
		/// <param name = "input">The integer to hash.</param>
		/// <returns>The hashed result.</returns>
		static int HashInt32(T input)
		{
			var x = (input as int?).Value;

			unchecked
			{
				x = ~x + (x << 15);
				// x = (x << 15) - x- 1, as (~x) + y is equivalent to y - x - 1 in two's complement representation
				x = x ^ (x >> 12);
				x = x + (x << 2);
				x = x ^ (x >> 4);
				x = x*2057; // x = (x + (x << 3)) + (x<< 11);
				x = x ^ (x >> 16);
				return x;
			}
		}

		/// <summary>
		/// 	Hashes a string using Bob Jenkin's "One At A Time" method from Dr. Dobbs (http://burtleburtle.net/bob/hash/doobs.html).
		/// 	Runtime is suggested to be 9x+9, where x = input.Length.
		/// </summary>
		/// <param name = "input">The string to hash.</param>
		/// <returns>The hashed result.</returns>
		static int HashString(T input)
		{
			var s = input as string;
			var hash = 0;

			unchecked 
			{
				for (var i = 0; i < s.Length; i++)
				{
					hash += s[i];
					hash += (hash << 10);
					hash ^= (hash >> 6);
				}
				hash += (hash << 3);
				hash ^= (hash >> 11);
				hash += (hash << 15);
			}

			return hash;
		}
	}
}