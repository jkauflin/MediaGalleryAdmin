using Dapper.Contrib.Extensions;

namespace MediaGalleryAdmin.Model
{
    [Table("MediaCategory")]
    public class MediaCategory
    {
        [Key]
        public int CategoryId { get; set; }
        public int MediaTypeId { get; set; }
        public string CategoryName { get; set; }
        public int CategoryOrder { get; set; }
	}
}
