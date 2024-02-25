using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Mathematics;

namespace MaxyGames.UNode.Editors {
	[ControlField(typeof(float2))]
	public class Float2Control : ValueControl {
		public Float2Control(ControlConfig config, bool autoLayout = false) : base(config, autoLayout) {
			Init();
		}

		void Init() {
			Vector2Field field = new Vector2Field() {
				value = config.value != null ? (float2)config.value : new Vector3(),
			};
			field.EnableInClassList("compositeField", false);

			field.RegisterValueChangedCallback((e) => {
				config.OnValueChanged((float2)e.newValue);
				MarkDirtyRepaint();
			});
			Add(field);
		}
	}
}