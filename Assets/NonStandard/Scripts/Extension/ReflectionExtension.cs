using System;
using System.Collections.Generic;
using System.Reflection;

public static class GetSubClassesExtension{
	public static Type[] GetSubClasses(this Type type) {
		Type[] allLocalTypes = Assembly.GetAssembly(type).GetTypes();
		List<Type> subTypes = new List<Type>();
		for(int i = 0; i < allLocalTypes.Length; ++i) {
			Type t = allLocalTypes[i];
			if(t.IsClass && !t.IsAbstract && t.IsSubclassOf(type)) { subTypes.Add(t); }
		}
		return subTypes.ToArray();
	}
}
