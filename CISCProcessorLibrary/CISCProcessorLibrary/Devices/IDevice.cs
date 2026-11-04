namespace CISCProcessorLibrary.Devices;

/// <summary>A peripheral attached to the processor's port bus and clock.</summary>
public interface IDevice
{
    /// <summary>Human readable name.</summary>
    string Name { get; }

    /// <summary>Returns the device to its power-on state.</summary>
    void Reset();

    /// <summary>Advances the device by the clock cycles the processor just consumed.</summary>
    void Tick(int cycles);
}
