using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;

namespace MaxyGames.UNode.Nodes {
	[NodeMenu("ECS", "Create IJobEntity", nodeName = "MyJob", scope = NodeScope.ECSGraph, icon = typeof(TypeIcons.RuntimeTypeIcon))]
	public class CreateIJobEntity : BaseJobNode, ISuperNodeWithEntry, IGeneratorPrePostInitializer {
		public enum DataKind {
			ReadOnly,
			ReadWrite,
			None,
		}
		public enum IndexKind {
			None,
			Entity,
			Chunk,
			ChunkAndEntity
		}
		public class Data {
			public string id = uNodeUtility.GenerateUID();

			public string name;
			public SerializedType type = typeof(IComponentData);
			public DataKind kind;

			[NonSerialized]
			public ValueOutput port;
		}
		public List<Data> datas = new List<Data>() { new Data() };

		public bool burstCompile = true;
		public EntityQueryOptions options = EntityQueryOptions.Default;
		public IndexKind indexKind;

		public List<SerializedType> withAll = new List<SerializedType>();
		public List<SerializedType> withAny = new List<SerializedType>();
		public List<SerializedType> withNone = new List<SerializedType>();
		public List<SerializedType> withChangeFilter = new List<SerializedType>();

		public ValueOutput entity { get; private set; }
		public ValueOutput index { get; private set; }

		public override string GetTitle() {
			return "Create Job: " + name;
		}

		public override void RegisterEntry(NestedEntryNode node) {
			base.RegisterEntry(node);

			entity = Node.Utilities.ValueOutput(node, nameof(entity), typeof(Entity), PortAccessibility.ReadOnly);

			switch(indexKind) {
				case IndexKind.None:
					//Cleanup if changed
					index = null;
					break;
				case IndexKind.Chunk:
					index = Node.Utilities.ValueOutput(node, nameof(index), typeof(int)).SetName("chunkIndexInQuery");
					break;
				case IndexKind.Entity:
					index = Node.Utilities.ValueOutput(node, nameof(index), typeof(int)).SetName("entityIndexInQuery");
					break;
				case IndexKind.ChunkAndEntity:
					index = Node.Utilities.ValueOutput(node, nameof(index), typeof(int));
					break;
			}

			for(int i = 0; i < datas.Count; i++) {
				var data = datas[i];
				data.port = Node.Utilities.ValueOutput(node, data.id, () => data.type, PortAccessibility.ReadWrite).SetName(!string.IsNullOrEmpty(data.name) ? data.name : ("Item" + (i + 1)));
				data.port.canSetValue = () => data.kind == DataKind.ReadWrite;
			}
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
			List<string> variableNames = datas.Select(item => CG.RegisterVariable(item.port)).ToList();

			for(int i = 0; i < datas.Count; i++) {
				int index = i;
				CG.RegisterPort(datas[index].port, () => variableNames[index]);
			}
			for(int i = 0; i < variableDatas.Count; i++) {
				var data = variableDatas[i];
				var vName = CG.RegisterVariable(data.port);
				CG.RegisterPort(data.port, () => vName);
			}

			if(entity.hasValidConnections) {
				var vName = CG.RegisterVariable(entity);
				CG.RegisterPort(entity, () => vName);
			}

			if(index != null && index.hasValidConnections) {
				var vName = CG.RegisterVariable(index);
				CG.RegisterPort(index, () => vName);
			}

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
				classBuilder.implementedInterfaces.Add(typeof(IJobEntity));
				classBuilder.SetToPartial();
				classBuilder.SetTypeToStruct();
				if(burstCompile && !CG.debugScript) {
					classBuilder.RegisterAttribute(typeof(BurstCompileAttribute));
				}
				if(localVariables.Count > 0) {
					for(int i = 0; i < localVariables.Count; i++) {
						var data = localVariables[i];
						classBuilder.RegisterVariable(CG.DeclareVariable(data.type, data.name, modifier: FieldModifier.PublicModifier));
					}
				}

				//Create execute method
				var method = new CG.MData(nameof(IJobChunk.Execute), typeof(void)) {
					modifier = new FunctionModifier(),
				};
				List<CG.MPData> parameters = new List<CG.MPData>();
				if(entity.hasValidConnections) {
					parameters.Add(new CG.MPData(CG.GetVariableName(entity), typeof(Entity)));
				}
				if(index != null && index.hasValidConnections) {
					CG.MPData paramData = new CG.MPData(CG.GetVariableName(index), typeof(int));

					switch(indexKind) {
						case IndexKind.Chunk:
							paramData.RegisterAttribute(typeof(EntityIndexInChunk));
							break;
						case IndexKind.ChunkAndEntity:
							paramData.RegisterAttribute(typeof(EntityIndexInChunk));
							paramData.RegisterAttribute(typeof(EntityIndexInQuery));
							break;
						case IndexKind.Entity:
							paramData.RegisterAttribute(typeof(EntityIndexInQuery));
							break;
					}
					parameters.Add(paramData);
				}
				for(int i = 0; i < variableNames.Count; i++) {
					var data = datas[i];
					switch(data.kind) {
						case DataKind.ReadOnly:
							parameters.Add(new CG.MPData(variableNames[i], data.type, RefKind.In));
							break;
						case DataKind.ReadWrite:
							parameters.Add(new CG.MPData(variableNames[i], data.type, RefKind.Ref));
							break;
						case DataKind.None:
							parameters.Add(new CG.MPData(variableNames[i], data.type));
							break;
					}
				}
				method.parameters = parameters;
				//Generate code for execute logic
				method.code = CG.GeneratePort(Entry.output);

				//Filters
				if(withAll.Count > 0) {
					classBuilder.RegisterAttribute(typeof(WithAllAttribute), withAll.Select(item => CG.Value(item.type)).ToArray());
				}
				if(withAny.Count > 0) {
					classBuilder.RegisterAttribute(typeof(WithAnyAttribute), withAny.Select(item => CG.Value(item.type)).ToArray());
				}
				if(withNone.Count > 0) {
					classBuilder.RegisterAttribute(typeof(WithNoneAttribute), withNone.Select(item => CG.Value(item.type)).ToArray());
				}
				if(withChangeFilter.Count > 0) {
					classBuilder.RegisterAttribute(typeof(WithChangeFilterAttribute), withChangeFilter.Select(item => CG.Value(item.type)).ToArray());
				}
				if(options != EntityQueryOptions.Default) {
					classBuilder.RegisterAttribute(typeof(WithOptionsAttribute), options.CGValue());
				}

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

	class CreateIJobEntityDrawer : NodeDrawer<Nodes.CreateIJobEntity> {
		static readonly FilterAttribute componentFilter;

		static CreateIJobEntityDrawer() {
			componentFilter = new FilterAttribute(typeof(IComponentData), typeof(IQueryTypeParameter)) {
				DisplayInterfaceType = false,
				DisplayReferenceType = true,
				DisplayValueType = true,
			};
		}

		public override void DrawLayouted(DrawerOption option) {
			var node = GetNode(option);

			UInspector.Draw(option.property[nameof(node.burstCompile)]);
			UInspector.Draw(option.property[nameof(node.options)]);
			UInspector.Draw(option.property[nameof(node.indexKind)]);

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
					node.variableDatas.Add(new Nodes.CreateIJobEntity.VData());
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

			uNodeGUI.DrawCustomList(node.datas, "Query",
				drawElement: (position, index, value) => {
					position.height = EditorGUIUtility.singleLineHeight;
					var portName = EditorGUI.DelayedTextField(position, new GUIContent("Item " + index), value.name);
					if(portName != value.name) {
						value.name = portName;
						node.Register();
						uNodeGUIUtility.GUIChanged(node, UIChangeType.Average);
						uNodeGUIUtility.GUIChanged(node.Entry, UIChangeType.Average);
					}
					position.y += EditorGUIUtility.singleLineHeight + 1;
					uNodeGUIUtility.DrawTypeDrawer(position, value.type, new GUIContent("Type"), type => {
						value.type = type;
						if(type.HasImplementInterface(typeof(IComponentData))) {
							if(value.kind == Nodes.CreateIJobEntity.DataKind.None) {
								value.kind = Nodes.CreateIJobEntity.DataKind.ReadWrite;
							}
						}
						else {
							value.kind = Nodes.CreateIJobEntity.DataKind.None;
						}
						node.Register();
						uNodeGUIUtility.GUIChanged(node, UIChangeType.Average);
						uNodeGUIUtility.GUIChanged(node.Entry, UIChangeType.Average);
					}, componentFilter, option.unityObject);
					position.y += EditorGUIUtility.singleLineHeight + 1;
					uNodeGUIUtility.EditValue(position, new GUIContent("Accessibility"), value.kind, (val) => {
						value.kind = val;
						if(value.kind == Nodes.CreateIJobEntity.DataKind.None && value.type.type.HasImplementInterface(typeof(IComponentData))) {
							value.kind = Nodes.CreateIJobEntity.DataKind.ReadWrite;
						}
						node.Register();
					});
				},
				add: position => {
					option.RegisterUndo();
					node.datas.Add(new Nodes.CreateIJobEntity.Data());
					node.Register();
					uNodeGUIUtility.GUIChanged(node, UIChangeType.Average);
					uNodeGUIUtility.GUIChanged(node.Entry, UIChangeType.Average);
				},
				remove: index => {
					option.RegisterUndo();
					node.datas.RemoveAt(index);
					node.Register();
					uNodeGUIUtility.GUIChanged(node, UIChangeType.Average);
					uNodeGUIUtility.GUIChanged(node.Entry, UIChangeType.Average);
				},
				elementHeight: index => {
					return (EditorGUIUtility.singleLineHeight * 3) + 3;
				});

			uNodeGUI.DrawTypeList("With All", node.withAll, componentFilter, node.GetUnityObject());
			uNodeGUI.DrawTypeList("With Any", node.withAny, componentFilter, node.GetUnityObject());
			uNodeGUI.DrawTypeList("With None", node.withNone, componentFilter, node.GetUnityObject());
			uNodeGUI.DrawTypeList("With Change Filter", node.withChangeFilter, componentFilter, node.GetUnityObject());

			DrawErrors(option);
		}
	}

	[NodeCustomEditor(typeof(Nodes.CreateIJobEntity))]
	class CreateIJobEntityView : BaseNodeView {
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