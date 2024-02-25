using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using Unity.Burst.Intrinsics;

namespace MaxyGames.UNode.Nodes {
	[NodeMenu("ECS", "Create IJobChunk", nodeName = "MyJob", scope = NodeScope.ECSGraph, icon = typeof(TypeIcons.RuntimeTypeIcon))]
	public class CreateIJobChunk : BaseJobNode, IGeneratorPrePostInitializer {

		public ValueOutput chunk { get; private set; }
		public ValueOutput unfilteredChunkIndex { get; private set; }
		public ValueOutput useEnabledMask { get; private set; }
		public ValueOutput chunkEnabledMask { get; private set; }

		public override void RegisterEntry(NestedEntryNode node) {
			chunk = Node.Utilities.ValueOutput<ArchetypeChunk>(node, nameof(chunk));
			unfilteredChunkIndex = Node.Utilities.ValueOutput<int>(node, nameof(unfilteredChunkIndex));
			useEnabledMask = Node.Utilities.ValueOutput<bool>(node, nameof(useEnabledMask));
			chunkEnabledMask = Node.Utilities.ValueOutput<v128>(node, nameof(chunkEnabledMask));

			base.RegisterEntry(node);
		}

		public override string GetTitle() {
			return "Create Job: " + name;
		}

		protected override void OnRegister() {
			Entry.Register();
		}

		public void OnPreInitializer() {
			//Ensure this node is registered
			this.EnsureRegistered();
			//Manual register the entry node.
			CG.RegisterEntry(entryObject);
			//Initialize the class name
			CG.RegisterUserObject(CG.GenerateNewName(name), this);
		}

		public void OnPostInitializer() {
			string className = CG.GetUserObject<string>(this);
			for(int i = 0; i < variableDatas.Count; i++) {
				var data = variableDatas[i];
				var vName = CG.RegisterVariable(data.port);
				CG.RegisterPort(data.port, () => vName);
			}
			var chunkCode = CG.GenerateNewName(nameof(chunk));
			var unfilteredChunkIndexCode = CG.GenerateNewName(nameof(unfilteredChunkIndex));
			var useEnabledMaskCode = CG.GenerateNewName(nameof(useEnabledMask));
			var chunkEnabledMaskCode = CG.GenerateNewName(nameof(chunkEnabledMask));
			CG.RegisterPort(chunk, () => chunkCode);
			CG.RegisterPort(unfilteredChunkIndex, () => unfilteredChunkIndexCode);
			CG.RegisterPort(useEnabledMask, () => useEnabledMaskCode);
			CG.RegisterPort(chunkEnabledMask, () => chunkEnabledMaskCode);

			List<ECSJobVariable> localVariables = JobVariables;
			if(variableDatas.Count > 0) {
				for(int i = 0; i < variableDatas.Count; i++) {
					var data = variableDatas[i];
					localVariables.Add(new ECSJobVariable() {
						name = CG.GetVariableName(data.port),
						type = data.type,
						owner = data,
					});
				}
			}

			CG.RegisterPostGeneration((classData) => {
				//Create class
				var classBuilder = new CG.ClassData(className);
				classBuilder.implementedInterfaces.Add(typeof(IJobChunk));
				classBuilder.SetToPartial();
				classBuilder.SetTypeToStruct();
				if(localVariables.Count > 0) {
					for(int i = 0; i < localVariables.Count; i++) {
						var data = localVariables[i];
						classBuilder.RegisterVariable(CG.DeclareVariable(data.type, data.name, modifier: FieldModifier.PublicModifier));
					}
				}

				//Create execute method
				var method = new CG.MData(nameof(IJobChunk.Execute), CG.Type(typeof(void))) {
					modifier = new FunctionModifier(),
				};
				List<CG.MPData> parameters = new List<CG.MPData> {
					new CG.MPData(chunkCode, CG.Type(typeof(ArchetypeChunk)), RefKind.In),
					new CG.MPData(unfilteredChunkIndexCode, CG.Type(typeof(int))),
					new CG.MPData(useEnabledMaskCode, CG.Type(typeof(bool))),
					new CG.MPData(chunkEnabledMaskCode, CG.Type(typeof(v128)), RefKind.In)
				};
				method.parameters = parameters;
				//Generate code for execute logic
				method.code = CG.GeneratePort(Entry.output);

				//Register the generated function code
				classBuilder.RegisterFunction(method.GenerateCode());
				//Register the generated type code
				classData.RegisterNestedType(CG.WrapWithInformation(classBuilder.GenerateCode(), this));
			});
		}
	}
}

#if UNITY_EDITOR
namespace MaxyGames.UNode.Editors {
	using UnityEditor;
	using UnityEditor.Experimental.GraphView;
	using UnityEngine.UIElements;

	class CreateIJobChunkDrawer : NodeDrawer<Nodes.CreateIJobChunk> {
		static readonly FilterAttribute componentFilter;

		static CreateIJobChunkDrawer() {
			componentFilter = new FilterAttribute(typeof(IComponentData), typeof(IQueryTypeParameter)) {
				DisplayInterfaceType = false,
				DisplayReferenceType = true,
				DisplayValueType = true,
			};
		}

		public override void DrawLayouted(DrawerOption option) {
			var node = GetNode(option);

			uNodeGUI.DrawCustomList(node.variableDatas, "Variables",
				drawElement: (position, index, value) => {
					position.height = EditorGUIUtility.singleLineHeight;
					var portName = EditorGUI.DelayedTextField(position, new GUIContent("Name "), value.name);
					if(portName != value.name) {
						value.name = portName;
						node.Register();
						uNodeGUIUtility.GUIChanged(node, UIChangeType.Important);
						uNodeGUIUtility.GUIChanged(node.Entry, UIChangeType.Average);
					}
					position.y += EditorGUIUtility.singleLineHeight + 1;
					uNodeGUIUtility.DrawTypeDrawer(position, value.type, new GUIContent("Type"), type => {
						value.type = type;
						node.Register();
						uNodeGUIUtility.GUIChanged(node, UIChangeType.Important);
						uNodeGUIUtility.GUIChanged(node.Entry, UIChangeType.Average);
					}, FilterAttribute.DefaultTypeFilter, option.unityObject);
				},
				add: position => {
					option.RegisterUndo();
					node.variableDatas.Add(new Nodes.CreateIJobChunk.VData());
					node.Register();
					uNodeGUIUtility.GUIChanged(node, UIChangeType.Important);
					uNodeGUIUtility.GUIChanged(node.Entry, UIChangeType.Average);
				},
				remove: index => {
					option.RegisterUndo();
					node.variableDatas.RemoveAt(index);
					node.Register();
					uNodeGUIUtility.GUIChanged(node, UIChangeType.Important);
					uNodeGUIUtility.GUIChanged(node.Entry, UIChangeType.Average);
				},
				elementHeight: index => {
					return (EditorGUIUtility.singleLineHeight * 2) + 2;
				});

			DrawErrors(option);
		}
	}

	[NodeCustomEditor(typeof(Nodes.CreateIJobChunk))]
	class CreateIJobChunkView : BaseNodeView {
		protected override void InitializeView() {
			base.InitializeView();
			var node = targetNode;
			{
				var element = new Button();
				element.text = "Create Run";
				AddControl(Direction.Input, element);
				element.clickable.clickedWithEventInfo += (evt) => {
					NodeEditorUtility.AddNewNode<Nodes.JobEntityExecutor>(node.nodeObject.parent, node.position.position, n => {
						n.runWith = Nodes.JobEntityExecutor.RunWith.Run;
						n.ReferenceNode = node as Nodes.BaseJobNode;
						//For refreshing the graph editor
						uNodeGUIUtility.GUIChanged(node.GetUnityObject(), UIChangeType.Important);
					});
				};
			}
			{
				var element = new Button();
				element.text = "Create Schedule";
				AddControl(Direction.Input, element);
				element.clickable.clickedWithEventInfo += (evt) => {
					NodeEditorUtility.AddNewNode<Nodes.JobEntityExecutor>(node.nodeObject.parent, node.position.position, n => {
						n.runWith = Nodes.JobEntityExecutor.RunWith.Schedule;
						n.ReferenceNode = node as Nodes.BaseJobNode;
						//For refreshing the graph editor
						uNodeGUIUtility.GUIChanged(node.GetUnityObject(), UIChangeType.Important);
					});
				};
			}
			{
				var element = new Button();
				element.text = "Create ScheduleParallel";
				AddControl(Direction.Input, element);
				element.clickable.clickedWithEventInfo += (evt) => {
					NodeEditorUtility.AddNewNode<Nodes.JobEntityExecutor>(node.nodeObject.parent, node.position.position, n => {
						n.runWith = Nodes.JobEntityExecutor.RunWith.ScheduleParallel;
						n.ReferenceNode = node as Nodes.BaseJobNode;
						//For refreshing the graph editor
						uNodeGUIUtility.GUIChanged(node.GetUnityObject(), UIChangeType.Important);
					});
				};
			}
		}

		public override void ReloadView() {
			base.ReloadView();
			border.EnableInClassList(ussClassBorderFlowNode, true);
			border.EnableInClassList(ussClassBorderOnlyInput, true);
		}
	}
}
#endif