using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;

namespace MaxyGames.UNode.Nodes {
	[NodeMenu("ECS/Flow", "Destroy Entity", scope = NodeScope.ECSGraph)]
	public class DestroyEntity : FlowNode {
		public ValueInput entity;

		protected override void OnExecuted(Flow flow) {
			throw new Exception("ECS is not supported in reflection mode.");
		}


		protected override void OnRegister() {
			base.OnRegister();
			entity = ValueInput(nameof(entity), typeof(Entity));
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
			var ecbName = ECSGraphUtility.GetECBSingleton<EndSimulationEntityCommandBufferSystem.Singleton>(this);
			CG.RegisterUserObject(ecbName, ("ecb", this));
			if(entities != null) {
				var variables = entities.JobVariables;
				if(variables != null) {
					entities.AddJobVariable(new ECSJobVariable() {
						name = ecbName,
						type = typeof(EntityCommandBuffer),
						value = () => ecbName,
						owner = typeof(EndSimulationEntityCommandBufferSystem.Singleton),
					});
				}
				CG.RegisterUserObject(entities, this);
			}
		}

		protected override string GenerateFlowCode() {
			if(entity.isAssigned) {
				var ecbName = CG.GetUserObject<string>(("ecb", this));
				var result = CG.Flow(
					ecbName.CGFlowInvoke(nameof(EntityCommandBuffer.DestroyEntity), CG.Value(entity)),
					CG.FlowFinish(enter, exit)
				);
				return result;
			}
			return CG.FlowFinish(enter, exit);
		}
	}
}