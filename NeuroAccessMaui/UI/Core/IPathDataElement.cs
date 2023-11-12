using Microsoft.Maui.Controls.Shapes;

namespace NeuroAccessMaui.UI.Core;

public interface IPathDataElement
{
	//note to implementor: implement this property publicly
	Geometry GeometryData { get; }
	Style PathStyle { get; }

	//note to implementor: but implement this method explicitly
	void OnGeometryDataPropertyChanged(Geometry OldValue, Geometry NewValue);
	void OnPathStylePropertyChanged(Style OldValue, Style NewValue);
}
