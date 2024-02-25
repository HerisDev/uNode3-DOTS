using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Mathematics;

namespace MaxyGames.UNode.Editors {
	[ControlField(typeof(float4))]
	public class Float4Control : ValueControl {
		public Float4Control(ControlConfig config, bool autoLayout = false) : base(config, autoLayout) {
			Init();
		}

		void Init() {
			Vector4Field field = new Vector4Field() {
				value = config.value != null ? (float4)config.value : new float4(),
			};
			field.EnableInClassList("compositeField", false);

			field.RegisterValueChangedCallback((e) => {
				config.OnValueChanged((float4)e.newValue);
				MarkDirtyRepaint();
			});
			Add(field);
		}
	}
}