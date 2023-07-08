namespace CocodriloDog.CD_JSON {

	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;
	using System;
	using System.Collections;
    using System.Collections.Generic;
	using System.Reflection;
	using System.Text.RegularExpressions;
	using UnityEngine;

	public static class CD_JSON {


		#region Public Static Methods

		/// <summary>
		/// Creates a JSON string representation of the provided object.
		/// </summary>
		/// <param name="obj">The object</param>
		/// <returns>The JSON string representation</returns>
        public static string Serialize(object obj, bool pretty = false) {
			JsonSerializerSettings settings = new JsonSerializerSettings();
			if (pretty) {
				settings.Formatting = Formatting.Indented;
			}
			return JsonConvert.SerializeObject(obj, settings);
		}

		/// <summary>
		/// Deserializes a JSON string and creates an instance of <typeparamref name="T"/>
		/// with the values stored in the string.
		/// </summary>
		/// <typeparam name="T">The type of object to be returned</typeparam>
		/// <param name="json">The JSON representation of the object</param>
		/// <returns>An instance of type <typeparamref name="T"/></returns>
		public static T Deserialize<T>(string json) where T : class{
			return JsonConvert.DeserializeObject<T>(json);
		}

		/// <summary>
		/// Deserializes a JSON string and creates an instance of <paramref name="type"/>
		/// with the values stored in the string.
		/// </summary>
		/// <param name="type">The type of object to be returned</param>
		/// <param name="json"></param>
		/// <returns>The JSON representation of the object</returns>
		public static object Deserialize(Type type, string json) {
			return null;
		}

		#endregion


	}

}