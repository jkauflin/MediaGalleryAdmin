using Dapper.Contrib.Extensions;

namespace MediaGalleryAdmin.Model
{
    [Table("People")]
    public class People
    {
        [Key]
        public int PeopleId { get; set; }
        public string PeopleName { get; set; }
	}
}
