using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using Object = UnityEngine.Object;
using System.Collections;
using Unity.Entities;
using Unity.Collections;
using UnityEngine.UIElements;
using MaxyGames.UNode.Nodes;

namespace MaxyGames.UNode.Editors {
    class AspectGraphManipulator : GraphManipulator {
		public override bool IsValid(string action) {
			return graph.HasImplementInterface(typeof(IAspect));
		}

		public override bool CreateNewVariable(Vector2 mousePosition, Action postAction) {
			ShowTypeMenu(
				mousePosition, 
				type => {
					void DoAction(Type type) {
						var variable = graph.GraphData.variableContainer.AddVariable("newVariable", type);
						variable.modifier.ReadOnly = true;
						GraphChanged();
					}
					if(type.HasImplementInterface(typeof(IComponentData))) {
						GenericMenu menu = new GenericMenu();
						menu.AddItem(new GUIContent($"RefRO<{type.PrettyName()}>"), false, () => {
							DoAction(typeof(RefRO<>).MakeGenericType(type));
						});
						menu.AddItem(new GUIContent($"RefRW<{type.PrettyName()}>"), false, () => {
							DoAction(typeof(RefRW<>).MakeGenericType(type));
						});
						menu.ShowAsContext();
					}
					else if(type.HasImplementInterface(typeof(IEnableableComponent))) {
						GenericMenu menu = new GenericMenu();
						menu.AddItem(new GUIContent($"EnabledRefRO<{type.PrettyName()}>"), false, () => {
							DoAction(typeof(EnabledRefRO<>).MakeGenericType(type));
						});
						menu.AddItem(new GUIContent($"EnabledRefRW<{type.PrettyName()}>"), false, () => {
							DoAction(typeof(EnabledRefRW<>).MakeGenericType(type));
						});
						menu.ShowAsContext();
					}
					else {
						DoAction(type);
					}
				}, 
				generalTypes: new[] {typeof(RefRO<>), typeof(RefRW<>), typeof(EnabledRefRO<>), typeof(EnabledRefRW<>), typeof(DynamicBuffer<>), typeof(Entity) },
				filter: new FilterAttribute() {
					OnlyGetType = true,
					ValidateType = type => {
						if(type.HasImplementInterface(typeof(IAspect)))
							return true;
						if(type.HasImplementInterface(typeof(IComponentData)))
							return true;
						if(type.HasImplementInterface(typeof(IEnableableComponent)))
							return true;
						if(type == typeof(Entity))
							return true;
						return false;
					}
				});
			return true;
		}

		public override IEnumerable<ContextMenuItem> ContextMenuForVariable(Vector2 mousePosition, Variable variable) {
			var type = variable.type;
			if(type.IsSubclassOfRawGeneric(typeof(RefRO<>)) || 
				type.IsSubclassOfRawGeneric(typeof(RefRW<>)) || 
				type.IsSubclassOfRawGeneric(typeof(EnabledRefRO<>)) || 
				type.IsSubclassOfRawGeneric(typeof(EnabledRefRW<>))) 
			{
				var elementType = type.GetGenericArguments()[0];
				return new ContextMenuItem[] {
					new DropdownMenuAction("Generate accessor properties (DOTS)", evt => {
						var items = ItemSelector.MakeCustomItems(elementType, new FilterAttribute() { ValidMemberType = MemberTypes.Field, ValidNextMemberTypes = 0 });
						ItemSelector.ShowCustomItem(items, member => {
							var prop = variable.graph.propertyContainer.NewProperty(member.Items.Last().GetActualName(), member.type);
							{//Getter
								prop.CreateGetter();
								var func = prop.getRoot;
								NodeEditorUtility.AddNewNode<Nodes.NodeReturn>(func, new Vector2(0, 100), (result) => {
									result.enter.ConnectTo(func.Entry.exit);
									NodeEditorUtility.AddNewNode(func, new Vector2(-300, 100), (MultipurposeNode value) => {
										value.target = member;
										result.value.ConnectTo(value.output);

										NodeEditorUtility.AddNewNode(func, new Vector2(-600, 100), (MultipurposeNode valueRO) => {
											valueRO.target = MemberData.CreateFromMember(type.GetMemberCached("ValueRO"));
											valueRO.instance.AssignToDefault(MemberData.CreateFromValue(variable));
											value.instance.ConnectTo(valueRO.output);
										});
									});
								});
							}
							if(type.IsSubclassOfRawGeneric(typeof(RefRW<>)) || type.IsSubclassOfRawGeneric(typeof(EnabledRefRW<>))) {
								prop.CreateSetter();
								var func = prop.setRoot;
								NodeEditorUtility.AddNewNode(func, new Vector2(0, 100), (NodeSetValue set) => {
									set.enter.ConnectTo(func.Entry.exit);
									set.value.ConnectToAsProxy(func.Entry.nodeObject.ValueOutputs.First());
									NodeEditorUtility.AddNewNode(func, new Vector2(-300, 100), (MultipurposeNode value) => {
										value.target = member;
										set.target.ConnectTo(value.output);

										NodeEditorUtility.AddNewNode(func, new Vector2(-600, 100), (MultipurposeNode valueRO) => {
											valueRO.target = MemberData.CreateFromMember(type.GetMemberCached("ValueRW"));
											valueRO.instance.AssignToDefault(MemberData.CreateFromValue(variable));
											value.instance.ConnectTo(valueRO.output);
										});
									});
								});
							}
							GraphChanged();
						}).ChangePosition(mousePosition);
					}, DropdownMenuAction.AlwaysEnabled)
				};
			}
			return null;
		}
	}
}