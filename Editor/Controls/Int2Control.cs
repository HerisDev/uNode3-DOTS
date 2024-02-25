using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Mathematics;

namespace MaxyGames.UNode.Editors {
	[ControlField(typeof(int2))]
	public class Int2Control : ValueControl {
		public Int2Control(ControlConfig config, bool autoLayout = false) : base(config, autoLayout) {
			Init();
		}

		void Init() {
			Vector2IntField field = new Vector2IntField() {
				value = config.value is int2 val ? new Vector2Int(val.x, val.y) : new Vector2Int(),
			};
			field.EnableInClassList("compositeField", false);

			field.RegisterValueChangedCallback((e) => {
				config.OnValueChanged(new int2(e.newValue.x, e.newValue.y));
				MarkDirtyRepaint();
			});
			Add(field);
		}
	}
}