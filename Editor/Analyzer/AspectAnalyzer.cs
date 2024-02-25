using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;
using Unity.Entities;
using Unity.Collections;

namespace MaxyGames.UNode.Editors.Analyzer {
    class AspectAnalyzer : GraphAnalyzer {
		public override bool IsValidAnalyzerForGraph(Type graphType) {
			return graphType == typeof(ClassScript);
		}

		public override void CheckGraphErrors(ErrorAnalyzer analyzer, IGraph graph) {
			ClassScript classScript = graph as ClassScript;
			if(classScript == null) return;
			if(classScript.HasImplementInterface(typeof(IAspect))) {
				var graphData = graph.GraphData;
				if(classScript.GetGraphInheritType() != typeof(ValueType)) {
					analyzer.RegisterError(graphData, "Aspect must be a `struct` type");
				}
				if(classScript.modifier.ReadOnly == false) {
					analyzer.RegisterError(graphData, $"Aspect must use `Read Only` modifier.");
				}
				if(classScript.modifier.Partial == false) {
					analyzer.RegisterError(graphData, "Aspect must use `Partial` modifier.");
				}
				var variables = graph.GetVariables().ToArray();
				if(variables.Length == 0) {
					analyzer.RegisterError(graphData, "An aspect struct must contain at least 1 variable of type RefRO<ComponentType>/RefRW<ComponentType> or embed another aspect.");
				}
				else {
					bool hasEntity = false;
					foreach(var member in variables) {
						if(member.modifier.ReadOnly == false) {
							analyzer.RegisterError(member, "Variable in aspect must use `Read Only` modifier.");
						}
						if(IsSupportedType(member.type) == false) {
							analyzer.RegisterError(member, "Aspects cannot contain variable of type other than RefRW<IComponentData>, RefRO<IComponentData>, EnabledRefRW<IComponentData>, EnabledRefRO<IComponentData>, DynamicBuffer<T>, or Entity");
						}
						else {
							if(member.attributes.Any(att => att.type == typeof(ReadOnlyAttribute)) && member.type.HasImplementInterface(typeof(RefRW<>))) {
								analyzer.RegisterError(member, "You may not use Unity.Collections.ReadOnlyAttribute on RefRW<IComponentData> variable. If you want read-only access to an IComponentData type, please use RefRO<IComponentData> instead");
							}
							if(member.type == typeof(Entity)) {
								if(hasEntity) {
									analyzer.RegisterError(member, "Aspects cannot contain more than one variable of type Unity.Entities.Entity");
								}
								else {
									hasEntity = true;
								}
							}
						}
					}
				}

				foreach(var member in graph.GetProperties()) {
					if(member.AutoProperty) {
						analyzer.RegisterError(member, "Aspects cannot contain auto properties");
					}
				}
			}
		}

		private static bool IsSupportedType(Type type) {
			if(type.IsSubclassOfRawGeneric(typeof(RefRO<>))) {
				return true;
			}
			if(type.IsSubclassOfRawGeneric(typeof(RefRW<>))) {
				return true;
			}
			if(type.IsSubclassOfRawGeneric(typeof(EnabledRefRO<>))) {
				return true;
			}
			if(type.IsSubclassOfRawGeneric(typeof(EnabledRefRW<>))) {
				return true;
			}
			if(type.IsSubclassOfRawGeneric(typeof(DynamicBuffer<>))) {
				return true;
			}
			if(type == typeof(Entity)) {
				return true;
			}
			if(type.HasImplementInterface(typeof(IAspect))) {
				return true;
			}
			return false;
		}
	}
}
