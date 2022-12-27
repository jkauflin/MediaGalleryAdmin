using Dapper.Contrib.Extensions;

namespace MediaGalleryAdmin.Model
{
    [Table("ConfigParam")]
    public class ConfigParamTable
    {
        [Key]
		public string ConfigName { get; set; }
        public string ConfigDesc { get; set; }
        public string ConfigValue { get; set; }
	}
}
