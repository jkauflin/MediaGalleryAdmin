using Dapper.Contrib.Extensions;

namespace MediaGalleryAdmin.Model
{
    [Table("MediaType")]
    public class MediaType
    {
        [Key]
		public int MediaTypeId { get; set; }
		public string MediaTypeDesc { get; set; }
        public string LocalRoot { get; set; }
	}
}
