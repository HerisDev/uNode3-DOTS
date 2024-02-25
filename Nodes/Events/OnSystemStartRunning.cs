using System;
using UnityEngine;
using System.Collections.Generic;
using Unity.Entities;

namespace MaxyGames.UNode.Nodes {
    [EventMenu("", "On Start Running", scope = NodeScope.ECSGraph)]
    public class OnSystemStartRunning : BaseECSEvent {
		protected override void OnRegister() {
			base.OnRegister();
		}

		public override void GenerateEventCode() {
			DoGenerateCode(nameof(ISystemStartStop.OnStartRunning));
		}
	}
}