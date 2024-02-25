using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;

namespace MaxyGames.UNode.Nodes {
    [NodeMenu("ECS/Flow", "Set Component Enabled", scope = NodeScope.ECSGraph)]
    public class SetComponentEnabled : FlowNode {
		public ValueInput entity;
		[Filter(typeof(IComponentData), DisplayAbstractType = false)]
		public SerializedType componentType;
		public ValueInput value;

		protected override void OnExecuted(Flow flow) {
			throw new Exception("ECS is not supported in reflection mode.");
		}

		protected override void OnRegister() {
			base.OnRegister();
			entity = ValueInput(nameof(entity), typeof(Entity));
			value = ValueInput(nameof(value), typeof(bool));
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
			if(entity.isAssigned) {
				var ecbName = CG.GetUserObject<string>(("ecb", this));
				if(ecbName != null) {
					var result = CG.Flow(
						ecbName.CGFlowInvoke(nameof(EntityCommandBuffer.SetComponentEnabled), new[] { componentType.type }, entity.CGValue(), value.CGValue()),
						CG.FlowFinish(enter, exit)
					);
					return result;
				}
				else {
					var result = CG.Flow(
						ecbName.CGFlowInvoke(nameof(SystemAPI.SetComponentEnabled), new[] { componentType.type }, entity.CGValue(), value.CGValue()),
						CG.FlowFinish(enter, exit)
					);
					return result;
				}
			}
			return CG.FlowFinish(enter, exit);
		}
	}
}