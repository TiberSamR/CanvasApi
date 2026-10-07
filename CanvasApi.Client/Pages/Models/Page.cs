namespace CanvasApi.Client.Pages.Models;

public class Page : IPage
{
    public string Url { get; set; }
    public string Title { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool? HideFromStudents { get; set; }
    // Canvas sends editing_roles as one comma-separated string ("teachers,students") and
    // last_edited_by as a User object. Typed as EditingRoles[] and string, every page read threw
    // a JsonSerializationException, which broke the tutor's CoursePage and CourseSyllabus tools.
    public string EditingRoles { get; set; }
    public JObject LastEditedBy { get; set; }
    public string Body { get; set; }
    public bool? Published { get; set; }
    public bool? FrontPage { get; set; }
    public bool? LockedForUser { get; set; }
    public JObject LockInfo { get; set; }
    public string LockExplanation { get; set; }
}