namespace ACadSharp.Entities
{
	public partial class TableEntity
	{
		public class CellContent
		{
			public ContentFormat Format { get; } = new ContentFormat();

			public TableCellContentType ContentType { get; set; }

			public CellValue Value { get; } = new CellValue();
		}
	}
}
