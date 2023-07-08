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
				return JSON_Null;
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
			var objJSON = "{";

			// Add CD_JSON special fields
			objJSON += $"\"cd_json_type\": \"{obj.GetType().FullName}\",";

			for (var i = 0; i < fieldInfos.Count; i++) {

				// Bypass Unity internal fields
				var isUnityInternal =
					fieldInfos[i].Name == "m_CachedPtr" ||
					fieldInfos[i].Name == "m_InstanceID" ||
					fieldInfos[i].Name == "m_UnityRuntimeErrorString";

				if (isUnityInternal) {
					break;
				}

				// This is the part that contains the name of the field
				var namePart = NamePart(fieldInfos[i]);

				// Format for leaf fields
				if (IsLeaf(fieldInfos[i].FieldType)) {
					objJSON += $"{namePart}{SerializeLeafValue(fieldInfos[i].GetValue(obj))}";
				}
				// Format for composite fields (arrays, lists and objects with properties)
				else {
					// Format for arrays and lists
					if (IsArrayOrList(fieldInfos[i].FieldType)) {
						objJSON += SerializeIEnumerable(obj, fieldInfos[i]);
					}
					// Format for non-list composite (Object with properties). Apply recursion
					else {
						var childObjString = Serialize(fieldInfos[i].GetValue(obj));
						if (childObjString == JSON_Null) {
							objJSON += $"{namePart}{childObjString}";
						} else {
							// TrimStart() removes the indent from the first curly brace
							objJSON += $"{namePart}{childObjString}";
						}
					}
				}
				// Add after each object
				objJSON += ",";
			}

			// Remove the last comma
			objJSON = RemoveLastComma(objJSON);

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

			/*
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
							// Limiting the split to 2, avoids error when parsing strings with ':'
							// For example: "m_SleepTime": "11:11 am"
							var line = jsonLines[i].Split(':', 2);
							var lineFieldName = Clean(line[0]);
							if(lineFieldName == "cd_json_type") {
								break;
							}
							var lineFieldValue = line[1];
							var lineFieldInfo = GetFieldInfo(instance.GetType(), lineFieldName);
							// If the json has an outdated property, this will ignore it and avoid an error
							// TODO: Apply this logic to the other JSONLineKinds
							if(lineFieldInfo == null) {
								break;
							}
							if(lineFieldInfo.FieldType == typeof(string)) {
								lineFieldValue = CleanStringValue(lineFieldValue);
							} else {
								lineFieldValue = Clean(lineFieldValue);
							}
							lineFieldInfo.SetValue(instance, DeserializeLeafOrNullValue(lineFieldValue, lineFieldInfo.FieldType));
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

							if(lineFieldInfo == null) {
								// TODO: At this point, if an outdated composite field is found, we must ignore it, 
								// but we must jump up to a point where the next property starts
							}

							if (IsArrayOrList(lineFieldInfo.FieldType)) {

								// First isolate and store JSON text that comprises the array or list
								//var arrayJSON = "";

								// Parse all lines between '[' and ']' which comprises the array or list json
								i++; // Skip the '[' character
								var nextLine = jsonLines[++i];
								var childArrayOrList = 0;

								// Create an array of strings with the serialized elements
								List<String> elementJSONs = new List<string>();

								while (GetJSONLineKind(nextLine) != JSONLineKind.SquareClose || childArrayOrList > 0) {
									// If a child array or list is found, this prevents the parsing to break
									// before the end of the main array or list
									if (GetJSONLineKind(nextLine) == JSONLineKind.SquareOpen) {
										childArrayOrList++;
									} else if (GetJSONLineKind(nextLine) == JSONLineKind.SquareClose) {
										childArrayOrList--;
									}
									// If there are no elements yet, we create the first one								
									if (elementJSONs.Count == 0) {
										elementJSONs.Add("");
									}
									// We add the next lines to the last element
									elementJSONs[elementJSONs.Count - 1] += nextLine + "\n";
									// Until we identify an end of element and then add a new element, but
									// we make sure that the end of element is not part of a child array or list
									if ((IsEndOfElementJSONLine(nextLine) && childArrayOrList == 0)) {
										elementJSONs.Add("");
									}
									nextLine = jsonLines[++i];
								}
								i--; // Go back to the ']' character

								// Create either the array or list
								Type arrayOrListType;
								if (lineFieldInfo.FieldType.IsArray) {
									// Create the array
									arrayOrListType = lineFieldInfo.FieldType.GetElementType();
									instance = Array.CreateInstance(arrayOrListType, elementJSONs.Count);
								} else {
									// Create the list
									arrayOrListType = lineFieldInfo.FieldType.GenericTypeArguments[0];
									var genericType = typeof(List<>).MakeGenericType(arrayOrListType);
									instance = Activator.CreateInstance(genericType);
									// Populate it with default value so that final values can be assigned via indexers []
									var listInstance = ((IList)instance);
									for (int j = 0; j < elementJSONs.Count; j++) {
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
								for (var k = 0; k < elementJSONs.Count; k++) {
									if (elementIsLeaf) {
										// Leaf object
										indexedInstance[k] = DeserializeLeafOrNullValue(Clean(elementJSONs[k]), arrayOrListType);
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
									if(objType == null) {
										Debug.Log($"NULL TYPE: {jsonLines[i + 2]}");
									}
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
			*/
			return null;
		}

		#endregion


		#region Private Constants

		private const string JSON_Null = "\"NULL\"";

		private const string JSON_True = "\"TRUE\"";

		private const string JSON_False = "\"FALSE\"";

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

			// Add the fields of the type
			fieldInfosList.AddRange(type.GetFields(BindingFlags));

			// Add the inherited fields too
			while (type.BaseType != null) {
				type = type.BaseType;
				fieldInfosList.AddRange(type.GetFields(BindingFlags));
			}

			// Remove non-serialized fields (otherwise they will be serialized)
			for(int i = fieldInfosList.Count - 1; i >= 0; i--) {
				if (fieldInfosList[i].IsNotSerialized) {
					fieldInfosList.RemoveAt(i);
				}
			}

			return fieldInfosList.ToArray();

		}

		/// <summary>
		/// Removes the last comma of the string, if any.
		/// </summary>
		/// <param name="str"></param>
		/// <returns></returns>
		private static string RemoveLastComma(string str) {
			int lastCommaIndex = str.LastIndexOf(',');
			if (lastCommaIndex >= 0) {
				str = str.Substring(0, lastCommaIndex) + str.Substring(lastCommaIndex + 1);
			}
			return str;
		}

		/// <summary>
		/// Serializes a leaf value, according to its type.
		/// </summary>
		/// <param name="leafValue"></param>
		/// <returns></returns>
		private static string SerializeLeafValue(object leafValue) {

			string valueJSON;
			if (leafValue is string) {
				valueJSON = $"\"{EscapeQuotes((string)leafValue)}\"";
			} else if (leafValue is bool) {
				valueJSON = (bool)leafValue ? JSON_True : JSON_False;
			} else {
				valueJSON = leafValue.ToString();
			}

			return valueJSON;

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
			iEnumerableJSON += $"{namePart}[";

			// Null list or array
			if (fieldValue == null) {
				return $"{namePart}{JSON_Null}";
			}
			
			// Add elements
			foreach (var element in (IEnumerable)fieldValue) {
				if (element == null) {
					iEnumerableJSON += JSON_Null;
				} else {
					// Leaf element
					if (IsLeaf(element.GetType())) {
						iEnumerableJSON += SerializeLeafValue(element);
					}
					// Composite element
					else {
						// Format for arrays and lists
						if (IsArrayOrList(element.GetType())) {
							// Unity does not serialize arrays of arrays, so this is not needed for now.
						}
						// Format for non-list composite (Object with properties). Apply recursion
						else {
							iEnumerableJSON += Serialize(element);
						}
					}
				}
				// Add after each element
				iEnumerableJSON += ",";
			}

			// Remove the last comma
			iEnumerableJSON = RemoveLastComma(iEnumerableJSON);

			// Close list
			iEnumerableJSON += $"]";

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
		private static string NamePart(FieldInfo fieldInfo) => $"\"{fieldInfo.Name}\": ";

		/// <summary>
		/// If there are any quotes in the text, escape them. For example <c>Hello, they call me "Monkey"</c>
		/// to <c>Hello, they call me \"Monkey\"</c> for compliancy with a valid JSON format.
		/// </summary>
		/// <param name="str"></param>
		/// <returns></returns>
		private static string EscapeQuotes(string str) => str.Replace("\"", "\\\"");

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
				if (i < lines.Length - 1) { // This avoids adding a new line after the closing curly brace
					indented += "\n";
				}
			}
			return indented;
		}

		#endregion


		#region Private Static Methods - Deserialize

		/// <summary>
		/// Gets the <see cref="FieldInfo"/> of a field named <paramref name="fieldName"/>
		/// in the <paramref name="type"/> and searches in the base types until it is found
		/// or <c>null</c> if none is found.
		/// </summary>
		/// <param name="type">The type</param>
		/// <param name="fieldName">The field name</param>
		/// <returns></returns>
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
		/// Determines whether this line is the end of an array or list element.
		/// </summary>
		/// <param name="jsonLine">The json line to analyse</param>
		/// <returns></returns>
		private static bool IsEndOfElementJSONLine(string jsonLine) => jsonLine.TrimEnd().Contains(',');

		/// <summary>
		/// Deserializes the provided <paramref name="stringValue"/> and returns an instance
		/// of the value in the specified <paramref name="type"/>.
		/// </summary>
		/// <param name="stringValue">The JSON string of the value</param>
		/// <param name="type">The type of the object to be returned</param>
		/// <returns>The deserialized value</returns>
		private static object DeserializeLeafOrNullValue(string stringValue, Type type) {
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

			var typeFullName = Clean(line.Split(':', 2)[1]);

			Type type = null;
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			foreach(var assembly in assemblies) {
				type = assembly.GetType(typeFullName);
				if(type != null) {
					break;
				}
			}

			return type;

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