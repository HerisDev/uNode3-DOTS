using System;
using UnityEngine;
using System.Collections.Generic;
using Unity.Entities;

namespace MaxyGames.UNode.Nodes {
    [EventMenu("", "On Create", scope = NodeScope.ECSGraph)]
    public class OnSystemCreate : BaseECSEvent {

		protected override void OnRegister() {
			base.OnRegister();
		}

		public override void GenerateEventCode() {
			DoGenerateCode(nameof(ISystem.OnCreate));
		}
	}
}