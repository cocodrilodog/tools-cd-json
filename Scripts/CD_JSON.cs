namespace CocodriloDog.CD_JSON {

	using System;
	using System.Collections;
    using System.Collections.Generic;
	using System.Reflection;
	using UnityEngine;


	#region Small Types

	public enum JSONLineKind {
		None,
		LeafOrNullField,
		CompositeFieldName,
		CurlyOpen,
		CurlyClose,
		SquareOpen,
		SquareClose,
	}

	#endregion


	public static class CD_JSON {


		#region Public Static Methods

		/// <summary>
		/// Creates a JSON string representation of the provided object.
		/// </summary>
		/// <param name="obj">The object</param>
		/// <returns>The JSON string representation</returns>
        public static string Serialize(object obj) {

			if (obj == null) {
				return "null";
			}

			// Get the fields of the root object
			var fieldInfos = new List<FieldInfo>(GetFieldInfos(obj.GetType()));

			// Remove Duplicates that result from inherited fields
			var fieldsDictionary = new Dictionary<string, FieldInfo>();
			foreach(var fieldInfo in fieldInfos) {
				fieldsDictionary[fieldInfo.Name] = fieldInfo;
			}
			fieldInfos = new List<FieldInfo>(fieldsDictionary.Values);

			// Open the string
			var objJSON = "{\n";

			// Add CD_JSON special fields
			objJSON += $"\t\"cd_json_type\":\"{obj.GetType().FullName}\"\n";

			for (var i = 0; i < fieldInfos.Count; i++) {

				var isUnityInternal =
					fieldInfos[i].Name == "m_CachedPtr" ||
					fieldInfos[i].Name == "m_InstanceID" ||
					fieldInfos[i].Name == "m_InstanceID" ||
					fieldInfos[i].Name == "m_UnityRuntimeErrorString";

				if (isUnityInternal) {
					break;
				}

				// This is the part that contains the name of the field
				var namePart = NamePart(fieldInfos[i]);

				// Format for leaf fields
				if (IsLeaf(fieldInfos[i].FieldType)) {
					if(fieldInfos[i].FieldType == typeof(string)) { // <- Strings have quotation
						objJSON += $"{namePart}\"{fieldInfos[i].GetValue(obj)}\"\n";
					} else {
						objJSON += $"{namePart}{fieldInfos[i].GetValue(obj)}\n"; 
					}
				}
				// Format for composite fields (arrays, lists and objects with properties)
				else {
					// Format for arrays and lists
					if (IsArrayOrList(fieldInfos[i].FieldType)) {
						objJSON += SerializeIEnumerable(obj, fieldInfos[i]);
					}
					// Format for non-list composite (Object with properties). Apply recursion
					else {
						var childObjString = $"{Serialize(fieldInfos[i].GetValue(obj))}";
						if (childObjString == "null") {
							objJSON += $"{namePart}{childObjString}\n";
						} else {
							objJSON += $"{namePart}\n{Indent(childObjString)}\n";
						}
					}
				}

			}

			//Close the string
			objJSON += "}";

			return objJSON;

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
		/// Deserializes a JSON string and creates an instance of <paramref name="type"/>
		/// with the values stored in the string.
		/// </summary>
		/// <param name="type">The type of object to be returned</param>
		/// <param name="json"></param>
		/// <returns>The JSON representation of the object</returns>
		public static object Deserialize(Type type, string json) {

			object instance;
			if (typeof(ScriptableObject).IsAssignableFrom(type)){
				instance = ScriptableObject.CreateInstance(type);
			} else {
				instance = Activator.CreateInstance(type);
			}

			Stack<ParentCompositeRef> parentRefStack = null;
			var jsonLines = json.Split('\n');

			for (var i = 0; i < jsonLines.Length; i++) {

				var lineKind = GetJSONLineKind(jsonLines[i]);

				switch (lineKind) {

					case JSONLineKind.LeafOrNullField: {
							var line = jsonLines[i].Split(':');
							var lineFieldName = Clean(line[0]);
							if(lineFieldName == "cd_json_type") {
								break;
							}
							var lineFieldValue = line[1];
							var lineFieldInfo = GetFieldInfo(instance.GetType(), lineFieldName);
							if(lineFieldInfo.FieldType == typeof(string)) {
								lineFieldValue = CleanStringValue(lineFieldValue);
							} else {
								lineFieldValue = Clean(lineFieldValue);
							}
							lineFieldInfo.SetValue(instance, DeserializeValue(lineFieldValue, lineFieldInfo.FieldType));
							// When there is a field like this: "fieldName": null, it classifies as
							// JSONLineKind.LeafOrNullField and in that case DeserializeValue() will return null
						}
						break;

					case JSONLineKind.CompositeFieldName: {

							// Get the info of this field
							var lineFieldName = Clean(jsonLines[i]);
							var lineFieldInfo = GetFieldInfo(instance.GetType(), lineFieldName);

							// Temporarily store the composite field instance to restore upon closing brace
							parentRefStack ??= new Stack<ParentCompositeRef>();
							parentRefStack.Push(new ParentCompositeRef(instance, lineFieldName));

							if (IsArrayOrList(lineFieldInfo.FieldType)) {

								//Debug.Log("ARRAY OR LIST");

								// First isolate and store JSON text that comprises the array or list
								var arrayJSON = "";

								// Parse all lines between '[' and ']'
								i++; // Skip the '[' character
								var nextLine = jsonLines[++i];
								var internalArrayOrList = 0;

								while (GetJSONLineKind(nextLine) != JSONLineKind.SquareClose || internalArrayOrList > 0) {
									if (GetJSONLineKind(nextLine) == JSONLineKind.SquareOpen) {
										internalArrayOrList++;
									} else if (GetJSONLineKind(nextLine) == JSONLineKind.SquareClose) {
										internalArrayOrList--;
									}
									// Add each line to the arrayJSON
									//Debug.Log($"{nextLine}");
									arrayJSON += nextLine + "\n";
									nextLine = jsonLines[++i];
								}
								i--; // Go back to the ']' character

								// Remove extra '\n' from the end
								arrayJSON = arrayJSON.TrimEnd();
								//Debug.Log($"RESULT:\n{arrayJSON}");

								// Create an array of strings with the serialized elements
								String[] elementJSONs;
								if (string.IsNullOrEmpty(arrayJSON)) {
									// When the array string is empty, we must create an empty string array. Otherwise the
									// code inside the else would create an array with one element out of the empty string.
									elementJSONs = new string[0];
								} else {
									// To parse the enumerable elements, separate the array JSON text by ','
									elementJSONs = arrayJSON.Split(',');
								}

								//for (var k = 0; k < elementJSONs.Length; k++) {
								//	Debug.Log($"ELEMENT JSON [{k}]:\n{elementJSONs[k]}");
								//}

								// Create either the array or list
								Type arrayOrListType;
								if (lineFieldInfo.FieldType.IsArray) {
									// Create the array
									arrayOrListType = lineFieldInfo.FieldType.GetElementType();
									instance = Array.CreateInstance(arrayOrListType, elementJSONs.Length);
								} else {
									// Create the list
									arrayOrListType = lineFieldInfo.FieldType.GenericTypeArguments[0];
									var genericType = typeof(List<>).MakeGenericType(arrayOrListType);
									instance = Activator.CreateInstance(genericType);
									// Populate it with default value so that final values can be assigned via indexers []
									var listInstance = ((IList)instance);
									for (int j = 0; j < elementJSONs.Length; j++) {
										if (arrayOrListType.IsValueType) {
											listInstance.Add(Activator.CreateInstance(arrayOrListType));
										} else {
											listInstance.Add(null);
										}
									}
								}

								// Set the values to the array or list
								// Check if element is leaf
								var elementIsLeaf = IsLeaf(arrayOrListType);
								// Make this cast to use the indexers []
								var indexedInstance = (IList)instance;
								for (var k = 0; k < elementJSONs.Length; k++) {
									if (elementIsLeaf) {
										// Leaf object
										indexedInstance[k] = DeserializeValue(Clean(elementJSONs[k]), arrayOrListType);
									} else {
										if (Clean(elementJSONs[k]) == "null") {
											indexedInstance[k] = null;
										} else {
											// Composite object
											//
											// After each ',', there is a '\n' so we remove it from the beginning of the
											// following element, starting at the second element
											elementJSONs[k] = elementJSONs[k].TrimStart('\n');
											// Get the element type from the "cd_json_type" field
											var elementType = GetTypeFromJSONLine(elementJSONs[k].Split('\n')[1]);
											// Recursion
											indexedInstance[k] = Deserialize(elementType, elementJSONs[k]);
										}
									}
								}

							} else {
								// Temporarily replace the instance to populate
								// This will make the following "fieldName" : value lines to be parsed as
								// fields that belong to this instance, instead of the composite one
								if (i < jsonLines.Length - 1 && Clean(jsonLines[i + 1]) == "null") {
									instance = null;
								} else {
									// Get the object type from the "cd_json_type" field
									var objType = GetTypeFromJSONLine(jsonLines[i + 2]);
									if (typeof(ScriptableObject).IsAssignableFrom(objType)) {
										instance = ScriptableObject.CreateInstance(objType);
									} else {
										instance = Activator.CreateInstance(objType);
									}
								}
							}

						}
						break;

					case JSONLineKind.CurlyClose:
					case JSONLineKind.SquareClose: {
							if (parentRefStack != null && parentRefStack.Count > 0) {
								// Get the parent reference
								var parentRef = parentRefStack.Pop();
								// Get the field of the children that belongs to the composites
								var child_fieldInfo = GetFieldInfo(parentRef.Instance.GetType(), parentRef.ChildFieldName);
								// Assign the child instance to the composite instance
								child_fieldInfo.SetValue(parentRef.Instance, instance);
								// Give control back to the composite instance
								instance = parentRef.Instance;
							}
						}
						break;

				}

			}

			return instance;

		}

		#endregion


		#region Private Static properties

		/// <summary>
		/// The <see cref="System.Reflection.BindingFlags"/> that will be used to 
		/// serialize/deserialize objects.
		/// </summary>
		private static BindingFlags BindingFlags => BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;


		#endregion


		#region Private Static Methods - General

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

		#endregion


		#region Private Static Methods - Serialize

		/// <summary>
		/// Finds all fields, including non-public and inherited.
		/// </summary>
		/// <param name="type">The type of the object</param>
		/// <returns>The field infos</returns>
		private static FieldInfo[] GetFieldInfos(Type type) {

			List<FieldInfo> fieldInfosList = new List<FieldInfo>();

			fieldInfosList.AddRange(type.GetFields(BindingFlags));
			while (type.BaseType != null) {
				type = type.BaseType;
				fieldInfosList.AddRange(type.GetFields(BindingFlags));
			}

			return fieldInfosList.ToArray();

		}

		private static FieldInfo GetFieldInfo(Type type, string fieldName) {
			var fieldInfo = type.GetField(fieldName, BindingFlags);
			var baseType = type;
			while (fieldInfo == null) {
				baseType = baseType.BaseType;
				if (baseType == null) {
					break;
				}
				fieldInfo = baseType.GetField(fieldName, BindingFlags);
			}
			return fieldInfo;
		}

		/// <summary>
		/// Creates a JSON string that represents the provided <paramref name="fieldInfo"/> that
		/// belongs to <paramref name="obj"/>.
		/// </summary>
		/// <param name="obj">The object that owns the field represented by <paramref name="fieldInfo"/></param>
		/// <param name="fieldInfo">A field info of an <see cref="IEnumerable"/> property</param>
		/// <returns>The JSON representation of the <see cref="IEnumerable"/> object</returns>
		private static string SerializeIEnumerable(object obj, FieldInfo fieldInfo) {

			// Some vars
			var fieldValue = fieldInfo.GetValue(obj);
			var namePart = NamePart(fieldInfo);
			var iEnumerableJSON = "";

			// Open list
			iEnumerableJSON += $"{namePart}\n\t[\n";
			if (fieldValue == null) {
				return $"{namePart}null\n";
			}
			// Add elements
			foreach (var element in (IEnumerable)fieldValue) {
				if (element == null) {
					iEnumerableJSON += $"{Indent(Indent("null"))},\n";
				} else {
					// Leaf element
					if (IsLeaf(element.GetType())) {
						if (element.GetType() == typeof(string)) { // <- Strings have quotation
							iEnumerableJSON += $"\t\t\"{element}\",\n";
						} else {
							iEnumerableJSON += $"\t\t{element},\n";
						}
					}
					// Composite element
					else {
						// Format for arrays and lists
						if (IsArrayOrList(element.GetType())) {
							// TODO: Array of array
							//objJSON += SerializeIEnumerable(obj, fieldInfos[i]);
						}
						// Format for non-list composite (Object with properties). Apply recursion
						else {
							var childObjString = $"{Serialize(element)}";
							iEnumerableJSON += $"{Indent(Indent(childObjString))},\n";
						}
					}
				}
			}
			// Remove the last '\n' and ','
			iEnumerableJSON = iEnumerableJSON.TrimEnd().TrimEnd(',');
			// Close list
			iEnumerableJSON += $"\n\t]\n"; // <- The first '\n' is replacing the previously removed '\n'

			return iEnumerableJSON;

		}

		/// <summary>
		///  Determines whether the type is "leaf" as understood in the composite pattern.
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		private static bool IsLeaf(Type type) => type.IsPrimitive || type == typeof(String);

		/// <summary>
		/// provides the format for the name part of the fields
		/// </summary>
		/// <param name="fieldInfo"></param>
		/// <returns></returns>
		private static string NamePart(FieldInfo fieldInfo) => $"\t\"{fieldInfo.Name}\": ";

		/// <summary>
		/// Indents the provided JSON by one tab.
		/// </summary>
		/// <param name="str"></param>
		/// <returns></returns>
		private static string Indent(string str) {
			var lines = str.Split('\n');
			var indented = "";
			for (var i = 0; i < lines.Length; i++) {
				indented += $"\t{lines[i]}";
				if (i < lines.Length - 1) { // <- This avoids adding a new line after the closing curly brace
					indented += "\n";
				}
			}
			return indented;
		}

		#endregion


		#region Private Static Methods - Deserialize

		/// <summary>
		/// Removes  '\n', '\r', '\t', ' ', '"', ':', ',' from the beginning and end of the string.
		/// </summary>
		/// <param name="str"></param>
		/// <returns></returns>
		private static string Clean(string str) => str.Trim(new Char[] { '\n', '\r', '\t', ' ', '"', ':', ',' });

		/// <summary>
		/// Removes leading and trailing whitespace and then removes the first and last characters
		/// that corresponds to "".
		/// </summary>
		/// <remarks>
		/// This was implemented to prevent removing the characters '\n', '\r', '\t', ' ', '"', ':', ','
		/// that actually belong the beginning and end of the string value and are beyond the quotations 
		/// of the JSON. Cases like this one: ""Some string"" or "...Some other string,".
		/// </remarks>
		/// <param name="str">The string value</param>
		/// <returns>The cleaned string value</returns>
		private static string CleanStringValue(string str) {
			str = str.Trim();
			str = str.Substring(1, str.Length - 2);
			return str;
		}

		/// <summary>
		/// Returns the kind of <paramref name="jsonLine"/>
		/// </summary>
		/// <param name="jsonLine">A JSON line</param>
		/// <returns>A <see cref="JSONLineKind"/></returns>
		private static JSONLineKind GetJSONLineKind(string jsonLine) {

			if (jsonLine.Contains(':')) {
				var jsonLine_trimmedEnd = jsonLine.TrimEnd();
				if (jsonLine_trimmedEnd[jsonLine_trimmedEnd.Length - 1] == ':') {
					return JSONLineKind.CompositeFieldName;
				} else {
					return JSONLineKind.LeafOrNullField;
				}
			}
			if (jsonLine.Contains('{')) return JSONLineKind.CurlyOpen;
			if (jsonLine.Contains('}')) return JSONLineKind.CurlyClose;
			if (jsonLine.Contains('[')) return JSONLineKind.SquareOpen;
			if (jsonLine.Contains(']')) return JSONLineKind.SquareClose;

			return JSONLineKind.None;

		}

		/// <summary>
		/// Deserializes the provided <paramref name="stringValue"/> and returns an instance
		/// of the value in the specified <paramref name="type"/>.
		/// </summary>
		/// <param name="stringValue">The JSON string of the value</param>
		/// <param name="type">The type of the object to be returned</param>
		/// <returns>The deserialized value</returns>
		private static object DeserializeValue(string stringValue, Type type) {
			if (type == typeof(string)) {
				return stringValue;
			}
			if (type == typeof(float)) {
				return float.Parse(stringValue);
			}
			if (type == typeof(double)) {
				return double.Parse(stringValue);
			}
			if (type == typeof(int)) {
				return int.Parse(stringValue);
			}
			if (type == typeof(bool)) {
				return bool.Parse(stringValue);
			}
			// Any other types passed here will return null. This is useful for cases like:
			// "fieldName": null
			return null;
		}

		/// <summary>
		/// Gets a type from a field like this one: "cd_json_type":"TypeFullName".
		/// </summary>
		/// <param name="line">The serialized line for that field</param>
		/// <returns>The type that corresponds to the type full name</returns>
		private static Type GetTypeFromJSONLine(string line) {
			var typeFullName = Clean(line.Split(':')[1]);
			var assembly = typeof(CD_JSON).Assembly;
			return assembly.GetType(typeFullName);
		}

		#endregion


	}

	public class ParentCompositeRef {


		#region Public Properties

		public object Instance => m_Instance;

		public string ChildFieldName => m_ChildFieldName;

		#endregion


		#region Public Constructors

		public ParentCompositeRef(object instance, string childFieldName) {
			m_Instance = instance;
			m_ChildFieldName = childFieldName;
		}

		#endregion


		#region Public Methods

		public override string ToString() {
			return $"[ParentCompositeRef: Instance:{Instance}; ChildFieldName:{ChildFieldName}]";
		}

		#endregion


		#region Private Fields

		private object m_Instance;
		
		private string m_ChildFieldName;
		
		#endregion

	}

}