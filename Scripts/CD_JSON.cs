namespace CocodriloDog.CD_JSON {

	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;
	using UnityEngine;

	public static class CD_JSON {


		#region Public Static Methods

		/// <summary>
		/// Creates a JSON string representation of the provided object.
		/// </summary>
		/// <param name="obj">The object</param>
		/// <returns>The JSON string representation</returns>
		public static string Serialize(object obj, bool prettyFormat = false) {
			JObject jObject = CreateJObject(obj);
			return jObject.ToString(prettyFormat ? Formatting.Indented : Formatting.None);
		}

		

		/// <summary>
		/// Deserializes a JSON string and creates an instance of <typeparamref name="T"/>
		/// with the values stored in the string.
		/// </summary>
		/// <typeparam name="T">The type of object to be returned</typeparam>
		/// <param name="json">The JSON representation of the object</param>
		/// <returns>An instance of type <typeparamref name="T"/></returns>
		public static T Deserialize<T>(string json) {
			return (T)Deserialize(typeof(T), json);
		}

		/// <summary>
		/// Deserializes a JSON string and creates an instance of <paramref name="targetType"/>
		/// with the values stored in the string.
		/// </summary>
		/// <param name="targetType">The type of object to be returned</param>
		/// <param name="json"></param>
		/// <returns>The JSON representation of the object</returns>
		public static object Deserialize(Type targetType, string json) {
			JObject obj = JObject.Parse(json);
			return CreateObject(targetType, obj);
		}

		#endregion


		#region Private Static properties

		/// <summary>
		/// The <see cref="System.Reflection.BindingFlags"/> that will be used to 
		/// serialize/deserialize objects.
		/// </summary>
		private static BindingFlags BindingFlags => BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;


		#endregion


		#region Private Static Methods - Serialize

		private static JObject CreateJObject(object obj) {

			JObject jObject = new JObject();
			Type objectType = obj.GetType();

			IEnumerable<FieldInfo> fields = GetFieldInfos(objectType);

			// Add this to support polymorphism
			jObject.Add("cd_json_type", objectType.FullName);

			// TODO: This should be configurable in a custom converter
			// Store the name of the ScriptableObject (it looks like it is not in any field)
			if (typeof(ScriptableObject).IsAssignableFrom(objectType)) {
				jObject.Add("m_Name", ((ScriptableObject)obj).name);
			}

			foreach (var field in fields) {

				string propertyName = field.Name;
				object propertyValue = field.GetValue(obj);

				// TODO: This should be configurable in a custom converter
				// Omit Unity internal values
				if (propertyName == "m_CachedPtr" ||
					propertyName == "m_InstanceID" ||
					propertyName == "m_UnityRuntimeErrorString") {
					continue;
				}

				if (propertyValue == null) {
					jObject.Add(propertyName, JValue.CreateNull());
					continue;
				}

				if (field.FieldType.IsArray) {

					JArray jArray = new JArray();
					Array array = (Array)propertyValue;

					foreach (object item in array) {
						JToken jToken = CreateJToken(item);
						jArray.Add(jToken);
					}

					jObject.Add(propertyName, jArray);

				} else if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>)) {

					JArray jArray = new JArray();
					IList list = (IList)propertyValue;

					foreach (object item in list) {
						JToken jToken = CreateJToken(item);
						jArray.Add(jToken);
					}

					jObject.Add(propertyName, jArray);

				} else {
					JToken jToken = CreateJToken(propertyValue);
					jObject.Add(propertyName, jToken);
				}
			}

			static JToken CreateJToken(object value) {

				if (value == null) {
					return JValue.CreateNull();
				}

				Type valueType = value.GetType();

				if (IsLeaf(valueType)) {
					return JToken.FromObject(value);
				} else if (value is IEnumerable enumerable) {

					JArray jArray = new JArray();

					foreach (object item in enumerable) {
						JToken jToken = CreateJToken(item);
						jArray.Add(jToken);
					}

					return jArray;
				} else {
					JObject jObject = CreateJObject(value);
					return jObject;
				}
			}

			return jObject;
		}

		#endregion


		#region Private Static Methods - Deserialize

		private static object CreateObject(Type objectType, JObject objToken) {

			string cdJsonTypeName = objToken["cd_json_type"]?.Value<string>();

			if (!string.IsNullOrEmpty(cdJsonTypeName)) {
				Type cdJsonType = Type.GetType(cdJsonTypeName);
				if (cdJsonType != null) {
					objectType = cdJsonType;
				}
			}

			// TODO: This should be configurable in a custom converter
			// Instantiate the ScriptableObject in a correct manner
			object instance = null;
			if (typeof(ScriptableObject).IsAssignableFrom(objectType)) {
				instance = ScriptableObject.CreateInstance(objectType);
			} else {
				instance = Activator.CreateInstance(objectType);
			}

			if (instance == null) {
				throw new InvalidOperationException($"Failed to create an instance of type: {cdJsonTypeName}");
			}

			// TODO: This should be configurable in a custom converter
			// Assign the name to the ScriptableObject
			if (instance is ScriptableObject) {
				((ScriptableObject)instance).name = objToken["m_Name"]?.Value<string>();
			}

			List<FieldInfo> fields = GetFieldInfos(objectType).ToList();

			foreach (JProperty property in objToken.Properties()) {

				if (property.Name == "cd_json_type") {
					continue;
				}

				FieldInfo field = fields.FirstOrDefault(f => f.Name == property.Name);
				if (field != null) {
					object value = ConvertValue(field.FieldType, property.Value);
					field.SetValue(instance, value);
				}

			}

			return instance;

			object ConvertValue(Type targetType, JToken valueToken) {

				if (IsArrayOrList(targetType)) {

					Type itemType;
					if (targetType.IsArray) {

						// Create the array
						itemType = targetType.GetElementType();
						var children = valueToken.Children();

						Array array = Array.CreateInstance(itemType, children.Count());
						int index = 0;
						foreach (JToken arrayItem in valueToken.Children()) {
							object listItem = ConvertValue(itemType, arrayItem);
							array.SetValue(listItem, index);
							index++;
						}

						return array;

					} else {

						// Create the list
						itemType = targetType.GetGenericArguments()[0];
						var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));
						foreach (JToken arrayItem in valueToken.Children()) {
							object listItem = ConvertValue(itemType, arrayItem);
							list.Add(listItem);
						}

						return list;

					}

				} else if (valueToken.Type == JTokenType.Object) {
					return CreateObject(targetType, valueToken as JObject);
				} else {
					if (valueToken.Type == JTokenType.Null) {
						return null;
					} else {
						return valueToken.ToObject(targetType);
					}
				}

			}
		}

		#endregion


		#region Private Static Methods - Utility

		/// <summary>
		/// Determines whether the <paramref name="type"/> is "leaf" as understood in the composite
		/// pattern. I.E. it can not contain children
		/// </summary>
		/// <param name="type">The type</param>
		/// <returns></returns>
		private static bool IsLeaf(Type type) => type.IsPrimitive || type == typeof(String);

		/// <summary>
		/// Is the provided <paramref name="type"/> an array or a generic list?
		/// </summary>
		/// <param name="type">The type</param>
		/// <returns><c>true</c> or <c>false</c></returns>
		private static bool IsArrayOrList(Type type) => type.IsArray || IsList(type);

		/// <summary>
		/// Is the provided <paramref name="type"/> a generic list?
		/// </summary>
		/// <param name="type">The type</param>
		/// <returns><c>true</c> or <c>false</c></returns>
		private static bool IsList(Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);

		// TODO: It seems that the inherited fields are already being included with just type.GetFields(BindingFlags)
		// however, GetFieldInfos is getting: m_CachedPtr, m_InstanceID, m_UnityRuntimeErrorString for some reason. We need 
		// to understand why

		/// <summary>
		/// Finds all fields, including non-public and inherited.
		/// </summary>
		/// <param name="type">The type of the object</param>
		/// <returns>The field infos</returns>
		private static FieldInfo[] GetFieldInfos(Type type) {

			List<FieldInfo> fieldInfosList = new List<FieldInfo>();

			// Add the fields of the type
			fieldInfosList.AddRange(type.GetFields(BindingFlags));

			// Add the inherited fields too
			while (type.BaseType != null) {
				type = type.BaseType;
				fieldInfosList.AddRange(type.GetFields(BindingFlags));
			}

			// Remove non-serialized fields (otherwise they will be serialized)
			for (int i = fieldInfosList.Count - 1; i >= 0; i--) {
				if (fieldInfosList[i].IsNotSerialized) {
					fieldInfosList.RemoveAt(i);
				}
			}

			// Return unique field infos
			return fieldInfosList.Distinct(new FieldInfoEqualityComparer()).ToArray();
		}

		#endregion


	}

	public class FieldInfoEqualityComparer : IEqualityComparer<FieldInfo> {


		#region Public Methods

		public bool Equals(FieldInfo x, FieldInfo y) {
			if (ReferenceEquals(x, y))
				return true;
			if (x is null || y is null)
				return false;
			return x.Name == y.Name;
		}

		public int GetHashCode(FieldInfo obj) {
			return obj.Name.GetHashCode();
		}

		#endregion


	}

}