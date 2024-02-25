using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Mathematics;

namespace MaxyGames.UNode.Editors {
	[ControlField(typeof(float3))]
	public class Float3Control : ValueControl {
		public Float3Control(ControlConfig config, bool autoLayout = false) : base(config, autoLayout) {
			Init();
		}

		void Init() {
			Vector3Field field = new Vector3Field() {
				value = config.value != null ? (float3)config.value : new float3(),
			};
			field.EnableInClassList("compositeField", false);

			field.RegisterValueChangedCallback((e) => {
				config.OnValueChanged((float3)e.newValue);
				MarkDirtyRepaint();
			});
			Add(field);
		}
	}
}