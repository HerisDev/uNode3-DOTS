using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.Collections.Generic;
using Unity.Entities;
using MaxyGames.UNode.Nodes;

namespace MaxyGames.UNode.Editors {
    public static class ECSEditorUtility {
		public static ClassScript CreateAspect(ScriptGraph scriptGraph, ClassScript component) {
			var authoringGraph = ScriptableObject.CreateInstance<ClassScript>();
			{
				authoringGraph.inheritType = typeof(MonoBehaviour);
				foreach(var variable in component.GetAllVariables()) {
					if(variable.type == typeof(Entity)) {
						authoringGraph.GraphData.variableContainer.NewVariable(variable.name, typeof(GameObject));
					}
					else if(variable.type.IsValueType) {
						authoringGraph.GraphData.variableContainer.NewVariable(variable.name, variable.type);
					}
				}
			}
			scriptGraph.TypeList.AddType(authoringGraph, scriptGraph);
			AssetDatabase.AddObjectToAsset(authoringGraph, scriptGraph);
			return authoringGraph;
		}

		public static ClassScript CreateBaker(ScriptGraph scriptGraph, Type componentType, Type authoringType) {
			var bakerGraph = ScriptableObject.CreateInstance<ClassScript>();
			{
				bakerGraph.name = componentType.Name + "Baker";
				bakerGraph.inheritType = ReflectionUtils.MakeGenericType(typeof(Baker<>), authoringType);
				var function = new Function(nameof(Baker.Bake), typeof(void), new[] { new ParameterData("authoring", authoringType) }) {
					modifier = new FunctionModifier() {
						Override = true,
					}
				};
				bakerGraph.GraphData.functionContainer.AddChild(function);
				{
					var entry = function.Entry;
					var authoringParameter = entry.nodeObject.ValueOutputs.First();

					NodeEditorUtility.AddNewNode(function, Vector2.zero, (CacheNode entity) => {
						entity.nodeObject.name = "entity";
						entry.exit.ConnectTo(entity.enter);

						NodeEditorUtility.AddNewNode(function, Vector2.zero, (NodeBaseCaller node) => {
							node.target = MemberData.CreateFromMember(typeof(IBaker).GetMethod(nameof(IBaker.GetEntity), new[] { typeof(TransformUsageFlags) }));
							node.parameters[0].input.AssignToDefault(TransformUsageFlags.Dynamic);
							entity.target.ConnectTo(node.output);
						});

						NodeEditorUtility.AddNewNode(function, Vector2.zero, (NodeBaseCaller addComponent) => {
							addComponent.target = MemberData.CreateFromMember(
								ReflectionUtils.MakeGenericMethod(
									typeof(IBaker).GetMethod(
										nameof(IBaker.AddComponent),
										1,
										new[]{
											typeof(Entity),
											Type.MakeGenericMethodParameter(0).MakeByRefType() }
										), 
									componentType)
								);
							addComponent.parameters[0].input.ConnectToAsProxy(entity.output);

							entity.exit.ConnectTo(addComponent.enter);

							NodeEditorUtility.AddNewNode(function, Vector2.zero, (MultipurposeNode ctor) => {
								ctor.target = MemberData.CreateFromMember(ReflectionUtils.GetDefaultConstructor(componentType));
								var initializers = ctor.initializers;
								var fields = componentType.GetFields();
								foreach(var field in fields) {
									initializers.Add(new MultipurposeMember.InitializerData() {
										name = field.Name,
										type = field.FieldType,
									});
								}
								addComponent.parameters[1].input.ConnectTo(ctor.output);
								ctor.Register();
								foreach(var init in initializers) {
									var type = init.type.type;
									if(type == typeof(Entity)) {
										NodeEditorUtility.AddNewNode(function, Vector2.zero, (NodeBaseCaller baseCaller) => {
											baseCaller.target = MemberData.CreateFromMember(typeof(IBaker).GetMethod(
												nameof(IBaker.GetEntity),
												new[] { typeof(GameObject), typeof(TransformUsageFlags) }));

											NodeEditorUtility.AddNewNode(function, Vector2.zero, (MultipurposeNode node) => {
												node.target = MemberData.CreateFromMember(authoringType.GetField(init.name));
												node.instance.ConnectToAsProxy(authoringParameter);
												baseCaller.parameters[0].input.ConnectTo(node.output);
											});
											baseCaller.parameters[1].input.AssignToDefault(TransformUsageFlags.Dynamic);
											init.port.ConnectTo(baseCaller.output);
										});
									}
									else {
										var field = authoringType.GetField(init.name);
										if(field != null) {
											NodeEditorUtility.AddNewNode(function, Vector2.zero, (MultipurposeNode node) => {
												node.target = MemberData.CreateFromMember(field);
												node.instance.ConnectToAsProxy(authoringParameter);
												init.port.ConnectTo(node.output);
											});
										}
									}
								}
							});
						});
					});
				}
			}
			scriptGraph.TypeList.AddType(bakerGraph, scriptGraph);
			AssetDatabase.AddObjectToAsset(bakerGraph, scriptGraph);
			return bakerGraph;
		}

		//For reference only
		abstract class Baker : Baker<GraphComponent> { }
	}
}