using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;

namespace MaxyGames.UNode.Nodes {
    public abstract class BaseJobNode : BaseEntryNode, ISuperNodeWithEntry, INodeEntitiesForeach {
		public class VData {
			public string id = uNodeUtility.GenerateUID();

			public string name = "variable";
			public SerializedType type = typeof(float);

			[NonSerialized]
			public ValueOutput port;
		}
		public List<VData> variableDatas = new List<VData>();

		public IEnumerable<NodeObject> NestedFlowNodes {
			get {
				yield return Entry;
			}
		}

		[SerializeField]
		protected NodeObject entryObject;
		public BaseEntryNode Entry {
			get {
				if(this == null) return null;
				if(entryObject == null || entryObject.node is not NestedEntryNode) {
					nodeObject.AddChild(entryObject = new NodeObject(new NestedEntryNode()));
				}
				return entryObject.node as BaseEntryNode;
			}
		}

		public List<ECSJobVariable> JobVariables {
			get {
				var result = CG.GetUserObject<List<ECSJobVariable>>(nodeObject);
				if(result == null) {
					result = new List<ECSJobVariable>();
					CG.RegisterUserObject(result, nodeObject);
				}
				return result;
			}
		}

		public virtual void RegisterEntry(NestedEntryNode node) {
			for(int i = 0; i < variableDatas.Count; i++) {
				var data = variableDatas[i];
				data.port = Node.Utilities.ValueOutput(node, data.id, () => data.type).SetName(data.name);
			}
		}

		public bool AllowCoroutine() {
			return false;
		}
	}
}