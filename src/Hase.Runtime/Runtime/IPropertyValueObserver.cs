namespace Hase.Runtime.Runtime;

/// <summary>
/// Receives notifications when a runtime property's value changes.
/// </summary>
public interface IPropertyValueObserver
{
    void OnPropertyValueChanged(PropertyValueChanged change);
}