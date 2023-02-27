using Dapper.Contrib.Extensions;

namespace MediaGalleryAdmin.Model
{
    [Table("Album")]
    public class Album
    {
        [Key]
        public int AlbumId { get; set; }
        public string AlbumName { get; set; }
	}
}
