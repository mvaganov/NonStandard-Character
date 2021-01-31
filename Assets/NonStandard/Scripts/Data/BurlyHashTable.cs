using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace NonStandard.Data {
	/// <summary>
	/// hash table that can have callbacks put on key/value pairs to notify when values change (keys are immutable)
	/// key/value pairs can also be Set to functions, meaning that they are calculated by code, including possibly other members.
	/// </summary>
	/// <typeparam name="KEY"></typeparam>
	/// <typeparam name="VAL"></typeparam>
	[Serializable] public class BurlyHashTable<KEY, VAL> : IDictionary<KEY, VAL> 
	//where VAL : IEquatable<VAL>
	where KEY : IComparable<KEY> {
		public Func<KEY, int> hFunc = null;
		public List<List<KV>> buckets;
		public const int defaultBuckets = 8;
		public List<KV> orderedPairs = new List<KV>();
		public Action<KEY, VAL, VAL> onChange;
		// TODO prototype fallback dictionary
		public enum ResultOfAssigningToFunction { ThrowException, Ignore, OverwriteFunction }
		public ResultOfAssigningToFunction onAssignmentToFunction = ResultOfAssigningToFunction.ThrowException;
		public void FunctionAssignIgnore() { onAssignmentToFunction = ResultOfAssigningToFunction.Ignore; }
		public void FunctionAssignException() { onAssignmentToFunction = ResultOfAssigningToFunction.ThrowException; }
		public void FunctionAssignOverwrite() { onAssignmentToFunction = ResultOfAssigningToFunction.OverwriteFunction; }
		int Hash(KEY key) { return Math.Abs(hFunc != null ? hFunc(key) : key.GetHashCode()); }
		[Serializable] public class KV {
			public int hash;
			public readonly KEY _key;
			public VAL _val;
			public BurlyHashTable<KEY, VAL> parent;
			public Action<VAL, VAL> onChange;

			public List<KV> dependents, reliesOn; // calculate which fields rely on this one
			private bool needsRecalculation = true;
			private Func<VAL> calc;
			private bool RemoveDependent(KV kv) { return (dependents != null) ? dependents.Remove(kv) : false; }
			private void AddDependent(KV kv) { if (dependents == null) { dependents = new List<KV>(); } dependents.Add(kv); }
			public Func<VAL> Calc {
				get { return calc; }
				set {
					calc = value;
					path.Clear();
					if (reliesOn != null) {
						reliesOn.ForEach(kv => kv.RemoveDependent(this));
						reliesOn.Clear();
					}
					watchingPath = true;
					_val = val;
					watchingPath = false;
					if (reliesOn == null) {
						reliesOn = new List<KV>();
					}
					path.Remove(this);
					reliesOn.AddRange(path);
					if (reliesOn != null) { reliesOn.ForEach(kv => kv.AddDependent(this)); }
					path.Clear();
				}
			}

			private static List<KV> path = new List<KV>();
			private static bool watchingPath = false;

			public KEY key { get { return _key; } }
			public VAL val {
				get {
					if (watchingPath) {
						if (path.Contains(this)) { throw new Exception("recursion: "+
							string.Join("->", path.ConvertAll(kv=>kv._key.ToString()).ToArray()) + "~>" + key);
						}
						path.Add(this);
						needsRecalculation = true;
					}
					if(calc != null && needsRecalculation) { SetInternal(calc.Invoke()); needsRecalculation = false; }
					return _val;
				}
				set {
					if (calc != null) {
						switch (parent.onAssignmentToFunction) {
						case ResultOfAssigningToFunction.ThrowException:
							string errorMessage = "can't set " + key + ", this value is calculated.";
							if (reliesOn != null) {
								errorMessage += " relies on: " + string.Join(", ", reliesOn.ConvertAll(kv => kv.key.ToString()).ToArray());
							}
							throw new Exception(errorMessage);
						case ResultOfAssigningToFunction.Ignore: return;
						case ResultOfAssigningToFunction.OverwriteFunction: calc = null; break;
						}
					}
					SetInternal(value);
				}
			}
			private void SetInternal(VAL value) {
				if ((_val == null && value != null) || (_val != null && !_val.Equals(value))) {
					if (dependents != null) dependents.ForEach(dep => dep.needsRecalculation = true);
					if (onChange != null) onChange.Invoke(_val, value);
					if (parent.onChange != null) parent.onChange.Invoke(_key, _val, value);
					_val = value;
				}
			}
			public KV(int hash, KEY k, BurlyHashTable<KEY, VAL> p) : this(hash, k, default(VAL), p) { }
			public KV(int h, KEY k, VAL v, BurlyHashTable<KEY, VAL> p) { parent = p; _key = k; _val = v; hash = h; }
			public override string ToString() { return key + "(" + hash + "):" + val; }
			public string ToString(bool showDependencies, bool showDependents) {
				StringBuilder sb = new StringBuilder();
				sb.Append(key).Append(":").Append(val);
				if (showDependencies) { showDependencies = reliesOn != null && reliesOn.Count != 0; }
				if (showDependents) { showDependents = dependents != null && dependents.Count != 0; }
				if (showDependencies || showDependents) {
					sb.Append(" /*");
					if (showDependencies) {
						sb.Append(" relies on: ");
						reliesOn.Join(sb, ", ", r=>r.key.ToString());
						//for(int i = 0; i < reliesOn.Count; ++i) { if(i>0) sb.Append(", "); sb.Append(reliesOn[i].key); }
					}
					if (showDependents) {
						sb.Append(" dependents: ");
						dependents.Join(sb, ", ", d => d.key.ToString());
						//for (int i = 0; i < dependents.Count; ++i) { if (i > 0) sb.Append(", "); sb.Append(dependents[i].key); }
					}
					sb.Append(" */");
				}
				return sb.ToString();
			}
			public class Comparer : IComparer<KV> {
				public int Compare(KV x, KV y) { return x.hash.CompareTo(y.hash); }
			}
			public static Comparer comparer = new Comparer();
			public static implicit operator KeyValuePair<KEY, VAL>(KV k) { return new KeyValuePair<KEY, VAL>(k.key, k.val); }
		}
		private KV Kv(KEY key) { return new KV(Hash(key), key, this); }
		private KV Kv(KEY key, VAL val) { return new KV(Hash(key), key, val, this); }
		public BurlyHashTable(Func<KEY, int> hashFunc, int bCount = defaultBuckets) { hFunc = hashFunc; BucketCount = bCount; }
		public BurlyHashTable() { }
		public BurlyHashTable(int bucketCount) { BucketCount = bucketCount; }
		public int Count {
			get {
				int sum = 0;
				if (buckets != null) { buckets.ForEach(bucket => sum += bucket != null ? bucket.Count : 0); }
				return sum;
			}
		}
		public int BucketCount { get { return buckets != null ? buckets.Count : 0; } set { SetHashFunction(hFunc, value); } }
		public Func<KEY, int> HashFunction { get { return hFunc; } set { SetHashFunction(value, BucketCount); } }
		public void SetHashFunction(Func<KEY, int> hFunc, int bucketCount) {
			this.hFunc = hFunc;
			if (bucketCount <= 0) { buckets = null; return; }
			List<List<KV>> oldbuckets = buckets;
			buckets = new List<List<KV>>(bucketCount);
			for (int i = 0; i < bucketCount; ++i) { buckets.Add(null); }
			if (oldbuckets != null) {
				oldbuckets.ForEach(b => { if (b != null) b.ForEach(kvp => Set(kvp.key, kvp.val)); });
			}
		}
		int FindExactIndex(KV kvp, int index, List<KV> list) {
			while (index > 0 && list[index - 1].hash == kvp.hash) { --index; }
			do {
				int compareValue = list[index].key.CompareTo(kvp.key);
				if (compareValue == 0) return index;
				if (compareValue > 0) return ~index;
				++index;
			} while (index < list.Count && list[index].hash == kvp.hash);
			return ~index;
		}
		public void FindEntry(KV kvp, out List<KV> bucket, out int bestIndexInBucket) {
			int whichBucket = kvp.hash % buckets.Count;
			bucket = buckets[whichBucket];
			if (bucket == null) { buckets[whichBucket] = bucket = new List<KV>(); }
			bestIndexInBucket = bucket.BinarySearch(kvp, KV.comparer);
			if (bestIndexInBucket < 0) { return; }
			bestIndexInBucket = FindExactIndex(kvp, bestIndexInBucket, bucket);
		}
		public bool Set(KEY key, VAL val) { return Set(Kv(key, val)); }
		public bool Set(KV kvp) {
			if (buckets == null) { BucketCount = defaultBuckets; }
			List<KV> bucket; int bestIndexInBucket;
			FindEntry(kvp, out bucket, out bestIndexInBucket);
			if (bestIndexInBucket < 0) {
				bucket.Insert(~bestIndexInBucket, kvp);
				orderedPairs.Add(kvp);
			} else {
				bucket[bestIndexInBucket].val = kvp.val;
			}
			return bestIndexInBucket < 0;
		}
		public bool Set(KEY key, Func<VAL> valFunc) {
			if (buckets == null) { BucketCount = defaultBuckets; }
			List<KV> bucket; int bestIndexInBucket;
			KV kvp = Kv(key);
			FindEntry(kvp, out bucket, out bestIndexInBucket);
			if (bestIndexInBucket < 0) {
				bestIndexInBucket = ~bestIndexInBucket;
				bucket.Insert(bestIndexInBucket, kvp);
				orderedPairs.Add(kvp);
			}
			bucket[bestIndexInBucket].Calc = valFunc;
			return true;
		}
		public bool TryGet(KEY key, out KV entry) {
			entry = Kv(key);
			if (buckets == null) return false;
			List<KV> bucket; int bestIndexInBucket;
			FindEntry(entry, out bucket, out bestIndexInBucket);
			if (bestIndexInBucket >= 0) {
				entry = bucket[bestIndexInBucket];
				return true;
			}
			return false;
		}
		public VAL Get(KEY key) {
			KV kvPair;
			if (TryGet(key, out kvPair)) { return kvPair.val; }
			throw new Exception("map does not contain key '"+key+"'");
		}
		public string ToDebugString() {
			StringBuilder sb = new StringBuilder();
			for (int b = 0; b < buckets.Count; ++b) {
				if (b > 0) sb.Append("\n");
				sb.Append(b.ToString()).Append(": ");
				List<KV> bucket = buckets[b];
				if (bucket != null) {
					for (int i = 0; i < bucket.Count; ++i) {
						if (i > 0) sb.Append(", ");
						sb.Append(bucket[i].ToString());
					}
				}
			}
			return sb.ToString();
		}
		public string Show(bool showCalcualted) {
			StringBuilder sb = new StringBuilder();
			bool printed = false;
			for (int i = 0; i < orderedPairs.Count; ++i) {
				if (showCalcualted || orderedPairs[i].Calc == null) {
					if (printed) sb.Append("\n");
					sb.Append(orderedPairs[i].ToString(true, false));
					printed = true;
				}
			}
			return sb.ToString();
		}
		/////////////////////////////////////////////// implementing IDictionary below ////////////////////////////////////////
		public ICollection<KEY> Keys { get { return orderedPairs.ConvertAll(kv => kv.key); } }
		public ICollection<VAL> Values { get { return orderedPairs.ConvertAll(kv => kv.val); } }
		public bool IsReadOnly { get { return false; } }
		public VAL this[KEY key] { get { return Get(key); } set { Set(key, value); } }
		public void Add(KEY key, VAL value) { Set(key, value); }
		public bool ContainsKey(KEY key) {
			List<KV> bucket;  int bestIndexInBucket;
			FindEntry(Kv(key), out bucket, out bestIndexInBucket);
			return bestIndexInBucket >= 0;
		}
		public bool Remove(KEY key) {
			List<KV> bucket; int bestIndexInBucket;
			FindEntry(Kv(key), out bucket, out bestIndexInBucket);
			if (bestIndexInBucket >= 0) {
				orderedPairs.Remove(bucket[bestIndexInBucket]);
				bucket.RemoveAt(bestIndexInBucket);
				return true;
			}
			return false;
		}
		public bool TryGetValue(KEY key, out VAL value) {
			KV found; if (TryGet(key, out found)) { value = found.val; return true; }
			value = default(VAL); return false;
		}
		public void Add(KeyValuePair<KEY, VAL> item) { Set(Kv(item.Key, item.Value)); }
		public void Clear() {
			if (buckets == null) return;
			for (int i = 0; i < buckets.Count; ++i) { if(buckets[i] != null) buckets[i].Clear(); }
			orderedPairs.Clear();
		}
		public bool Contains(KeyValuePair<KEY, VAL> item) {
			List<KV> bucket; int bestIndex;
			FindEntry(Kv(item.Key), out bucket, out bestIndex);
			return bestIndex >= 0 && bucket[bestIndex].val.Equals(item.Value);
		}
		public void CopyTo(KeyValuePair<KEY, VAL>[] array, int arrayIndex) {
			int index = arrayIndex;
			for (int i = 0; i < orderedPairs.Count; ++i) { array[index++] = orderedPairs[i]; }
		}
		public bool Remove(KeyValuePair<KEY, VAL> item) {
			List<KV> bucket; int bestIndex;
			FindEntry(Kv(item.Key), out bucket, out bestIndex);
			if (bestIndex >= 0 && item.Value.Equals(bucket[bestIndex].val)) {
				orderedPairs.Remove(bucket[bestIndex]);
				bucket.RemoveAt(bestIndex);
				return true;
			}
			return false;
		}
		public IEnumerator<KeyValuePair<KEY, VAL>> GetEnumerator() { return new Enumerator(this); }
		IEnumerator IEnumerable.GetEnumerator() { return new Enumerator(this); }
		public class Enumerator : IEnumerator<KeyValuePair<KEY, VAL>> {
			BurlyHashTable<KEY, VAL> htable;
			int index = -1; // MoveNext() is always called before the enumeration begins
			public Enumerator(BurlyHashTable<KEY, VAL> htable) { this.htable = htable; }
			public KeyValuePair<KEY, VAL> Current { get { return htable.orderedPairs[index]; } }
			object IEnumerator.Current { get { return Current; } }
			public void Dispose() { htable = null; }
			public bool MoveNext() {
				if (htable.orderedPairs == null || index >= htable.orderedPairs.Count) return false;
				return ++index < htable.orderedPairs.Count;
			}
			public void Reset() { index = -1; }
		}
	}
}