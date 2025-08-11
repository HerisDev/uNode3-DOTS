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
	class ComponentGraphManipulator : GraphManipulator {
		public override bool IsValid(string action) {
			return graph is ClassScript && graph.HasImplementInterface(typeof(IComponentData));
		}

		public override IEnumerable<ContextMenuItem> ContextMenuForGraph(Vector2 mousePosition) {
			var componentGraph = graph as ClassScript;
			return new ContextMenuItem[] {
				new DropdownMenuAction("Generate Baker and Authoring component", evt => {
					var path = AssetDatabase.GetAssetPath(componentGraph);
					var dir = System.IO.Path.GetDirectoryName(path);
					var scriptGraph = ScriptableObject.CreateInstance<ScriptGraph>();
					scriptGraph.Namespace = componentGraph.Namespace;
					scriptGraph.UsingNamespaces = new List<string>(componentGraph.GetUsingNamespaces());
					AssetDatabase.CreateAsset(scriptGraph, dir + "/" + componentGraph.GraphName + "Authoring.asset");
					var authoringGraph = ECSEditorUtility.CreateAspect(scriptGraph, componentGraph);
					ECSEditorUtility.CreateBaker(scriptGraph, componentGraph.GetGraphType(), authoringGraph.GetGraphType());
					AssetDatabase.SaveAssetIfDirty(scriptGraph);
				}, DropdownMenuAction.AlwaysEnabled)
			};
		}
	}
}