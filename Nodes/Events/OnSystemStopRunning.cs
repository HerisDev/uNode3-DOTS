using System;
using UnityEngine;
using System.Collections.Generic;
using Unity.Entities;

namespace MaxyGames.UNode.Nodes {
    [EventMenu("", "On Stop Running", scope = NodeScope.ECSGraph)]
    public class OnSystemStopRunning : BaseECSEvent {
		protected override void OnRegister() {
			base.OnRegister();
		}

		public override void GenerateEventCode() {
			DoGenerateCode(nameof(ISystemStartStop.OnStopRunning));
		}
	}
}