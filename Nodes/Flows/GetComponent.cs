using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;

namespace MaxyGames.UNode.Nodes {
    [NodeMenu("ECS/Data", "Get Component", scope = NodeScope.ECSGraph)]
    public class GetComponent : ValueNode {
		public ValueInput entity;
		[Filter(typeof(IComponentData), DisplayAbstractType = false)]
		public SerializedType componentType;

		protected override void OnRegister() {
			base.OnRegister();
			entity = ValueInput(nameof(entity), typeof(Entity));
		}

		public override Type ReturnType() => componentType;

		public override string GetRichTitle() {
			return $"Get Component: {componentType.GetRichName()}";
		}

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
			if(entities != null) {
				var variables = entities.JobVariables;
				if(variables != null) {
					var ecbName = ECSGraphUtility.GetEntityManager(this);
					entities.AddJobVariable(new ECSJobVariable() {
						name = ecbName,
						type = typeof(EntityManager),
						value = () => ecbName,
						owner = typeof(EntityManager),
					});
					CG.RegisterUserObject(ecbName, ("ecb", this));
					CG.RegisterUserObject(entities, this);
				}
			}
		}

		protected override string GenerateValueCode() {
			if(entity.isAssigned && componentType.isAssigned) {
				var ecbName = CG.GetUserObject<string>(("ecb", this));
				if(ecbName != null) {
					return ecbName.CGInvoke(nameof(EntityManager.GetComponentData), new[] { componentType.type }, entity.CGValue());
				}
				else {
					return ecbName.CGInvoke(nameof(SystemAPI.GetComponent), new[] { componentType.type }, entity.CGValue());
				}
			}
			return null;
		}
	}
}