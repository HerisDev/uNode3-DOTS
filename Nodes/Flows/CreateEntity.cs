using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;

namespace MaxyGames.UNode.Nodes {
    [NodeMenu("ECS/Data", "Create Entity", scope = NodeScope.ECSGraph)]
    public class CreateEntity : ValueNode {

		protected override void OnRegister() {
			base.OnRegister();
		}

		public override Type ReturnType() => typeof(Entity);

		public override void OnGeneratorInitialize() {
			base.OnGeneratorInitialize();
			var conenctions = CG.Nodes.FindAllConnections(this, false, false, true, true);
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

		protected override string GenerateValueCode() {
			var ecbName = CG.GetUserObject<string>(("ecb", this));
			return ecbName.CGInvoke(nameof(EntityCommandBuffer.CreateEntity));
		}
	}
}