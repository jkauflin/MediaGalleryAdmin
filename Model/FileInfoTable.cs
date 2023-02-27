using Dapper.Contrib.Extensions;

namespace MediaGalleryAdmin.Model
{
    [Table("FileInfo")]
    public class FileInfoTable
    {
        [ExplicitKey]
        public string Name { get; set; }
        public int MediaTypeId { get; set; }
        public string CategoryTags { get; set; }
        public string MenuTags { get; set; }
        public string AlbumTags { get; set; }
        public string FullNameLocal { get; set; }
        public string NameAndPath { get; set; }
        public string FilePath { get; set; }
        public DateTime CreateDateTime { get; set; }
        public DateTime LastModified { get; set; }
        public DateTime TakenDateTime { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string People { get; set; }
        public int ToBeProcessed { get; set; }
    }
}
