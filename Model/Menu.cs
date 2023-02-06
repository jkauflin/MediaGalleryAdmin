using Dapper.Contrib.Extensions;

namespace MediaGalleryAdmin.Model
{
    [Table("Menu")]
    public class Menu
    {
        [Key]
        public int MenuId { get; set; }
        public int CategoryId { get; set; }
        public string MenuItem { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string SearchStr { get; set; }
	}
}
