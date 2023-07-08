namespace CocodriloDog.CD_JSON.Examples {

	using Newtonsoft.Json;
	using System.Collections;
	using System.Collections.Generic;
	using UnityEngine;

	[JsonObject(memberSerialization: MemberSerialization.Fields)]
	[CreateAssetMenu(menuName = "Cocodrilo Dog/CD JSON/Examples/CD JSON Polymorphic Derived")]
	public class CD_JSON_PolymorphicDerived : CD_JSON_PolymorphicBase {


		#region Public Fields

		[SerializeField]
		public string DerivedField;

		#endregion


	}

}