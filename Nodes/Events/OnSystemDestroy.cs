using System;
using UnityEngine;
using System.Collections.Generic;
using Unity.Entities;

namespace MaxyGames.UNode.Nodes {
    [EventMenu("", "On Destroy", scope = NodeScope.ECSGraph)]
    public class OnSystemDestroy : BaseECSEvent {

		protected override void OnRegister() {
			base.OnRegister();
		}

		public override void GenerateEventCode() {
			DoGenerateCode(nameof(ISystem.OnDestroy));
		}
	}
}