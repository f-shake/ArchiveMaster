namespace ArchiveMaster.Models;

public class TypoSegment
{
    public string Text { get; set; }
    public bool HasTypo {  get; set; }
    public TypoItem Typo { get; set; }  
}
