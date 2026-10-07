namespace CanvasApi.Client.Pages.Models;

public class ListOptions : IListOptions
{
    public PageSort Sort { get; set; }
    public SortOrder Order { get; set; }
    public string SearchTerm { get; set; }
    // Nullable so it is only sent when set. As a plain bool it defaulted to false and every list
    // went out as published=false, which made Canvas return ONLY unpublished pages — so a list with
    // no options came back empty for students, who cannot see unpublished pages at all.
    public bool? Published { get; set; }
}