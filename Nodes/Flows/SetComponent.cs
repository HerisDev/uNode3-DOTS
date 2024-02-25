using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;

namespace MaxyGames.UNode.Nodes {
    [NodeMenu("ECS/Flow", "Set Component", scope = NodeScope.ECSGraph)]
    public class SetComponent : FlowNode {
		public ValueInput entity;
		public ValueInput component;

		protected override void OnExecuted(Flow flow) {
			throw new Exception("ECS is not supported in reflection mode.");
		}

		protected override void OnRegister() {
			base.OnRegister();
			entity = ValueInput(nameof(entity), typeof(Entity));
			component = ValueInput(nameof(component), typeof(IComponentData));
		}

		public override void OnGeneratorInitialize() {
			base.OnGeneratorInitialize();
			var conenctions = CG.Nodes.FindAllConnections(this, false, false, true, false);
			INodeEntitiesForeach entities = null;
			foreach(var node in conenctions) {
				if(entities == null && node.node is INodeEntitiesForeach) {
					entities = node.node as INodeEntitiesForeach;
					break;
				}
			}
			if(entities == null) {
				entities = nodeObject.GetNodeInParent<INodeEntitiesForeach>();
			}
			if(entities != null) {
				var variables = entities.JobVariables;
				if(variables != null) {
					var ecbName = ECSGraphUtility.GetECBSingleton<EndSimulationEntityCommandBufferSystem.Singleton>(this);
					entities.AddJobVariable(new ECSJobVariable() {
						name = ecbName,
						type = typeof(EntityCommandBuffer),
						value = () => ecbName,
						owner = typeof(EndSimulationEntityCommandBufferSystem.Singleton),
					});
					CG.RegisterUserObject(ecbName, ("ecb", this));
					CG.RegisterUserObject(entities, this);
				}
			}
		}

		protected override string GenerateFlowCode() {
			if(entity.isAssigned && component.isAssigned) {
				var ecbName = CG.GetUserObject<string>(("ecb", this));
				if(ecbName != null) {
					var result = CG.Flow(
						ecbName.CGFlowInvoke(nameof(EntityCommandBuffer.SetComponent), new[] { component.ValueType }, entity.CGValue(), component.CGValue()),
						CG.FlowFinish(enter, exit)
					);
					return result;
				}
				else {
					var result = CG.Flow(
						ecbName.CGFlowInvoke(nameof(SystemAPI.SetComponent), new[] { component.ValueType }, entity.CGValue(), component.CGValue()),
						CG.FlowFinish(enter, exit)
					);
					return result;
				}
			}
			return CG.FlowFinish(enter, exit);
		}

		public override void CheckError(ErrorAnalyzer analyzer) {
			base.CheckError(analyzer);
			if(component.isAssigned) {
				if(component.ValueType.IsValueType == false) {
					analyzer.RegisterError(this, "Component must be assigned to Value Type ( struct )");
				}
			}
		}
	}
}