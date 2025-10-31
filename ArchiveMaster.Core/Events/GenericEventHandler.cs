namespace ArchiveMaster.Events;

public delegate void GenericEventHandler<T>(object sender, GenericEventArgs<T> e);