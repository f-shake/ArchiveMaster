namespace ArchiveMaster.ViewModels.FileSystem
{
    public class WriteOncePackageCollection
    {
        public List<WriteOnceFile> OutOfSizeFiles { get; init; }
        public List<WriteOncePackage> Packages { get; init; }
    }
}
