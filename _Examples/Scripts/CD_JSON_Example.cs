namespace CocodriloDog.CD_JSON.Examples {
	
    using System;
	using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    public class CD_JSON_Example : MonoBehaviour {


		#region Unity Methods

		private void Start() {
			
			// Class object
			var serializedClass1 = CD_JSON.Serialize(m_Class1, true);
			Debug.Log($"serializedClass1:\n{serializedClass1}");
			var deserializedClass1 = CD_JSON.Deserialize<Class1>(serializedClass1);
			Debug.Log($"deserializedClass1:{deserializedClass1}");

			// ScriptableObject
			m_OriginalSO.m_NullList = null;
			m_OriginalSO.m_FinalNullList = null;
			var serializedSO = CD_JSON.Serialize(m_OriginalSO, true);
			Debug.Log($"serializedSO:\n{serializedSO}");
			m_DeserializedSO = CD_JSON.Deserialize<CD_JSON_ScriptableObject>(serializedSO);
			Debug.Log($"m_DeserializedSO: {m_DeserializedSO}");

			// TODO: Incomplete Json

			// TODO: Unexpected fields in Json

			// TODO: Multiple references

		}

		#endregion


		#region Private Static Methods

		private static string ListToString(IList list) {
			var str = "";
			if (list != null && list.Count > 0) {
				for (var i = 0; i < list.Count - 1; i++) {
					str += $"{list[i]}, ";
				}
				str += $"{list[list.Count - 1]}";
			}
			return $"[{str}]";
		}

		#endregion


		#region Private Fields

		[SerializeField]
		private Class1 m_Class1;

		[SerializeField]
		private CD_JSON_ScriptableObject m_OriginalSO;

		[SerializeField]
		private CD_JSON_ScriptableObject m_DeserializedSO;

		#endregion


		[Serializable]
		public class Class1 {


			#region Public Fields

			[SerializeField]
			public string SomeString1;

			[SerializeField]
			public float SomeFloat1;

			[SerializeField]
			public double SomeDouble1;

			[SerializeField]
			public int SomeInt1;

			[SerializeField]
			public bool SomeBool1;

			[SerializeField]
			public Class2 SomeClass2_1;

			[SerializeField]
			public string[] SomeStringArray1;

			[SerializeField]
			public List<string> SomeStringList1;

			[SerializeField]
			public float[] SomeFloatArray1;

			[SerializeField]
			public List<float> SomeFloatList1;

			[SerializeField]
			public Class4[] SomeClass4Array;

			[SerializeField]
			public string FinalString;

			#endregion


			#region Public Methods

			public override string ToString() {
				return $"({SomeString1}, {SomeFloat1}, {SomeDouble1}, {SomeInt1}, {SomeBool1}, {SomeClass2_1}, " +
					$"{ListToString(SomeStringArray1)}, " +
					$"{ListToString(SomeStringList1)}, " +
					$"{ListToString(SomeFloatArray1)}, " +
					$"{ListToString(SomeFloatList1)}), " +
					$"{ListToString(SomeClass4Array)})";
			}

			#endregion


			#region Private Fields

			[NonSerialized]
			private string m_NonSerializedField;

			#endregion


		}

		[Serializable]
		public class Class2 {


			#region Public Fields

			[SerializeField]
			public string SomeString2;

			[SerializeField]
			public float SomeFloat2;

			[SerializeField]
			public double SomeDouble2;

			[SerializeField]
			public int SomeInt2;

			[SerializeField]
			public bool SomeBool2;

			[SerializeField]
			public string[] SomeStringArray2;

			[SerializeField]
			public List<string> SomeStringList2;

			[SerializeField]
			public Class3 SomeClass3_2;

			[SerializeField]
			public bool ExtraBool2;

			#endregion


			#region Public Methods

			public override string ToString() {
				return $"({SomeString2}, {SomeFloat2}, {SomeDouble2}, {SomeInt2}, {SomeBool2}, " +
					$"{ListToString(SomeStringArray2)} " +
					$"{ListToString(SomeStringList2)} " +
					$"{SomeClass3_2}, {ExtraBool2})";
			}

			#endregion


		}

		[Serializable]
		public class Class3 {


			#region Public Fields

			[SerializeField]
			public string SomeString3;

			[SerializeField]
			public float SomeFloat3;

			[SerializeField]
			public double SomeDouble3;

			[SerializeField]
			public int SomeInt3;

			[SerializeField]
			public bool SomeBool3;

			#endregion


			#region Public Methods

			public override string ToString() {
				return $"({SomeString3}, {SomeFloat3}, {SomeDouble3}, {SomeInt3}, {SomeBool3})";
			}

			#endregion


		}

		[Serializable]
		public class Class4 {


			#region Public Methods

			[SerializeField]
			public string SomeString4;

			[SerializeField]
			public float SomeFloat4;

			[SerializeField]
			public bool SomeBool4;

			#endregion


			#region Public Methods

			public override string ToString() {
				return $"({SomeString4}, {SomeFloat4}, {SomeBool4})";
			}

			#endregion


		}

	}

}