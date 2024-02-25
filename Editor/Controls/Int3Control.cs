using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Mathematics;

namespace MaxyGames.UNode.Editors {
	[ControlField(typeof(int3))]
	public class Int3Control : ValueControl {
		public Int3Control(ControlConfig config, bool autoLayout = false) : base(config, autoLayout) {
			Init();
		}

		void Init() {
			Vector3IntField field = new Vector3IntField() {
				value = config.value is int3 val ? new Vector3Int(val.x, val.y, val.z) : new Vector3Int(),
			};
			field.EnableInClassList("compositeField", false);

			field.RegisterValueChangedCallback((e) => {
				config.OnValueChanged(new int3(e.newValue.x, e.newValue.y, e.newValue.z));
				MarkDirtyRepaint();
			});
			Add(field);
		}
	}
}