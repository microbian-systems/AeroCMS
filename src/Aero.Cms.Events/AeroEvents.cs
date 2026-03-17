namespace Aero.Cms.Events;

public abstract record AeroEvent(string message);
public abstract record AeroEvent<T>(T record);