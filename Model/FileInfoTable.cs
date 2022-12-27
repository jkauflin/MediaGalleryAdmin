using Dapper.Contrib.Extensions;

namespace MediaGalleryAdmin.Model
{
    [Table("FileInfo")]
    public class FileInfoTable
    {
        [ExplicitKey]
        public string Name { get; set; }
        public string Category { get; set; }
        public string FullNameLocal { get; set; }
        public string NameAndPath { get; set; }
        public DateTime CreateDateTime { get; set; }
        public DateTime LastModified { get; set; }
        public DateTime TakenDateTime { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string People { get; set; }
        public int ToBeProcessed { get; set; }
    }
}
