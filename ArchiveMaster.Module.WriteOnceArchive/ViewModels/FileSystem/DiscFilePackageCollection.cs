namespace ArchiveMaster.ViewModels.FileSystem
{
    public class WriteOncePackageCollection
    {
        public List<WriteOncePackage> Packages { get; init; } 
        public List<WriteOnceFile> OutOfSizeFiles { get; init; }
    }
}
