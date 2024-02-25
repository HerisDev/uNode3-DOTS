using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;

namespace MaxyGames.UNode.Nodes {
	public class JobEntityExecutor : FlowNode {
		public enum RunWith {
			Run,
			Schedule,
			ScheduleParallel,
		}

		public RunWith runWith;

		//Use node object, because the serializer not support dirrect referencing.
		[SerializeField]
		private NodeObject reference;
		public BaseJobNode ReferenceNode {
			get {
				if(reference != null && reference.node is BaseJobNode node) {
					return node;
				}
				return null;
			}
			set {
				reference = value;
			}
		}

		public ValueInput query { get; set; }
		public ValueInput dependsOn { get; set; }
		public ValueInput chunkBaseEntityIndices { get; set; }
		public ValueOutput jobHandle { get; set; }
		public ValueInput[] variablePorts { get; set; }

		protected override void OnRegister() {
			base.OnRegister();
			if(ReferenceNode != null) {
				ReferenceNode.EnsureRegistered();
				var datas = ReferenceNode.variableDatas;
				variablePorts = new ValueInput[datas.Count];
				for(int i = 0; i < variablePorts.Length; i++) {
					var data = datas[i];
					variablePorts[i] = ValueInput(data.id, () => data.type).SetName(data.name);
				}
			}
			switch(runWith) {
				case RunWith.Run:
					query = ValueInput(nameof(query), typeof(EntityQuery));
					break;
				case RunWith.Schedule:
					query = ValueInput(nameof(query), typeof(EntityQuery));
					dependsOn = ValueInput(nameof(dependsOn), typeof(Unity.Jobs.JobHandle));
					break;
				case RunWith.ScheduleParallel:
					query = ValueInput(nameof(query), typeof(EntityQuery));
					dependsOn = ValueInput(nameof(dependsOn), typeof(Unity.Jobs.JobHandle));
					if(dependsOn.isAssigned) {
						chunkBaseEntityIndices = ValueInput(nameof(chunkBaseEntityIndices), typeof(Unity.Collections.NativeArray<int>));
					}
					break;
			}
			if(dependsOn != null && dependsOn.isAssigned) {
				jobHandle = ValueOutput(nameof(jobHandle), typeof(Unity.Jobs.JobHandle));
			}
		}

		public override string GetTitle() {
			switch(runWith) {
				case RunWith.Run:
					return $"Job.Run";
				case RunWith.Schedule:
					return $"Job.Schedule";
				case RunWith.ScheduleParallel:
					return $"Job.ScheduleParallel";
			}
			return base.GetTitle();
		}

		protected override void OnExecuted(Flow flow) {
			throw new NotImplementedException();
		}

		public override void OnGeneratorInitialize() {
			base.OnGeneratorInitialize();
			if(jobHandle != null && jobHandle.hasValidConnections) {
				string varName = CG.RegisterVariable(jobHandle);
				CG.RegisterPort(jobHandle, () => varName);
			}
		}

		protected override string GenerateFlowCode() {
			List<string> parameters = new List<string>(3);
			if(query.isAssigned) {
				parameters.Add(CG.GeneratePort(query));
			}
			if(dependsOn != null && dependsOn.isAssigned) {
				parameters.Add(CG.GeneratePort(dependsOn));
			}
			if(chunkBaseEntityIndices != null && chunkBaseEntityIndices.isAssigned) {
				parameters.Add(CG.GeneratePort(chunkBaseEntityIndices));
			}
			List<string> initializers = null;
			{
				var jobVariables = ReferenceNode.JobVariables;
				initializers = new List<string>(jobVariables.Count);
				foreach(var variable in jobVariables) {
					if(variable.value != null) {
						initializers.Add(CG.SetValue(variable.name, variable.value()));
					}
					else if(variable.value == null && variable.owner is BaseJobNode.VData vdata) {
						var port = variablePorts.FirstOrDefault(p => p.name == vdata.name);
						if(port != null) {
							initializers.Add(CG.SetValue(variable.name, CG.GeneratePort(port)));
						}
					}
				}
			}
			string job = CG.GenerateName("job", this);
			string result = CG.DeclareVariable(job, CG.New(CG.GetUserObject<string>(ReferenceNode), null, initializers));

			if(jobHandle != null && jobHandle.hasValidConnections) {
				result = CG.Flow(
					result,
					CG.DeclareVariable(
						CG.GetVariableName(jobHandle), 
						job.CGInvoke(runWith.ToString(), parameters.ToArray()))
				);
			}
			else {
				result = CG.Flow(
					result, 
					job.CGFlowInvoke(runWith.ToString(), parameters.ToArray())
				);
			}
			return CG.Flow(result, CG.FlowFinish(enter, exit));
		}

		public override void CheckError(ErrorAnalyzer analyzer) {

		}
	}
}


#if UNITY_EDITOR
namespace MaxyGames.UNode.Editors {
	using UnityEditor;
	using UnityEditor.Experimental.GraphView;
	using UnityEngine.UIElements;

	[NodeCustomEditor(typeof(Nodes.JobEntityExecutor))]
	class JobEntityExecutorView : BaseNodeView {
		protected override void OnSetup() {
			base.OnSetup();
			var node = targetNode as Nodes.JobEntityExecutor;
			{
				var element = new Button();
				if(node.ReferenceNode != null) {
					element.text = node.ReferenceNode.name;
				}
				else {
					element.text = "None";
				}
				element.clickable.clickedWithEventInfo += (evt) => {
					if(node.ReferenceNode != null) {
						uNodeEditor.HighlightNode(node.ReferenceNode);
					}
					else {
						if(nodeObject.parent != null) {
							GenericMenu menu = new GenericMenu();
							foreach(var n in nodeObject.parent.GetNodesInChildren<Nodes.BaseJobNode>()) {
								var jobNode = n;
								menu.AddItem(new GUIContent(n.name), false, () => {
									node.ReferenceNode = jobNode;
								});
							}
							menu.ShowAsContext();
						}
					}
				};

				element.AddManipulator(new ContextualMenuManipulator(evt => {
					if(nodeObject.parent != null) {
						foreach(var n in nodeObject.parent.GetNodesInChildren<Nodes.BaseJobNode>()) {
							var jobNode = n;
							evt.menu.AppendAction(n.name, act => {
								node.ReferenceNode = jobNode;
							});
						}
					}
				}));

				titleContainer.Add(element);
			}
		}
	}
}
#endif